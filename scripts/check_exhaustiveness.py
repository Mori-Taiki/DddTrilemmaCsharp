"""実装の一時コピーにケースを追加し、コンパイル時の保証を検証する。"""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
src = root / "src"
dotnet = str(Path(sys.argv[1]).resolve()) if len(sys.argv) > 1 else "dotnet"

for variant, model, setter_diagnostic in [
    ("Immutable", "不変版", "CS0200"),
    ("Mutable", "可変版", "CS0272"),
]:
    with tempfile.TemporaryDirectory(prefix="ddd-type-check-") as directory:
        target = Path(directory)
        for source in (src / "Domain").glob("*.cs"):
            shutil.copy(source, target)
        shutil.copy(src / "Domain" / variant / "ストーリー.cs", target)
        for name in ["境界値.cs", "閲覧判定サービス.cs"]:
            shutil.copy(src / "Api" / name, target)
        shutil.copy(root / "global.json", target / "global.json")
        project = target / "Probe.csproj"
        project.write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <WarningsAsErrors>CS8509</WarningsAsErrors>
  </PropertyGroup>
</Project>
''')

        def build(restore=False):
            command = [dotnet, "build", str(project), "--disable-build-servers", "--nologo"]
            if not restore:
                command.append("--no-restore")
            return subprocess.run(command, cwd=root, text=True, capture_output=True)

        baseline = build(restore=True)
        if baseline.returncode:
            sys.exit(baseline.stdout + baseline.stderr)
        print(f"PASS [{model}]: 現在の実装はコンパイルできる", flush=True)

        for name in ["公開範囲", "料金区分", "閲覧判定", "事実要求", "閲覧結果"]:
            extra = target / "追加ケース.cs"
            extra.write_text(f"namespace Demo.Domain; public sealed record 追加した{name} : {name};")
            result = build()
            if result.returncode == 0 or "error CS8509" not in result.stdout:
                sys.exit(f"FAIL: {name}の追加漏れを検出できない\n{result.stdout}{result.stderr}")
            extra.unlink()
            print(f"PASS [{model}]: {name}のケース追加はCS8509で検出される", flush=True)

        for name, source, diagnostic in [
            ("利用者IDとストーリーIDは取り違えられない",
             "public static void Run(ストーリー story) => 閲覧.開始(story, new ストーリーID(\"s\"));", "CS1503"),
            ("公開範囲を外から直接書き換えられない",
             "public static void Run(ストーリー story) { story.公開範囲 = new 公開範囲.全体公開(); }", setter_diagnostic),
        ]:
            extra = target / "不正な操作.cs"
            extra.write_text(f"using Demo.Domain; public static class Probe {{ {source} }}")
            result = build()
            if result.returncode == 0 or f"error {diagnostic}" not in result.stdout:
                sys.exit(f"FAIL: {name}\n{result.stdout}{result.stderr}")
            extra.unlink()
            print(f"PASS [{model}]: {name}", flush=True)
