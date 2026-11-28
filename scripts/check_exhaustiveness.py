"""実装の一時コピーにケース追加や不正な接続を行い、コンパイル時の保証を検証する。"""
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[1]
dotnet = str(Path(sys.argv[1]).resolve()) if len(sys.argv) > 1 else "dotnet"


def check(implementation, variant, model, setter_diagnostic):
    src = root / "src" / implementation
    model = f"{implementation}/{model}"
    resume = "再開する"
    with tempfile.TemporaryDirectory(prefix="ddd-type-check-") as directory:
        target = Path(directory)
        for source in (src / "Domain").glob("*.cs"):
            shutil.copy(source, target)
        shutil.copy(src / "Domain" / variant / "ストーリー.cs", target)
        for name in ["境界変換.cs", "閲覧判定サービス.cs"]:
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

        invalid_operations = [
            ("利用者IDとストーリーIDは取り違えられない",
             "public static void Run(ストーリー story) => 閲覧判定.開始(story, new ストーリーID(\"s\"));", "CS1503"),
            ("公開範囲を外から直接書き換えられない",
             "public static void Run(ストーリー story) { story.公開範囲 = new 公開範囲.全体公開(); }", setter_diagnostic),
        ]

        fact_sources = [
            ("フォロー確認が必要", "facts.フォローしているか(new 利用者ID(\"reader\"), new 利用者ID(\"author\"), default)"),
            ("VIP会員確認が必要", "facts.VIP会員か(request.閲覧者, default)"),
            ("購入確認が必要", "facts.購入済みか(new 利用者ID(\"reader\"), new ストーリーID(\"story\"), default)"),
        ]
        for request_type, _ in fact_sources:
            for other_type, query in fact_sources:
                if request_type == other_type:
                    continue
                invalid_operations.append((
                    f"{request_type}に{other_type}の取得結果は渡せない",
                    f"public static async Task<閲覧判定> Run({request_type} request, Demo.Api.閲覧事実取得 facts) "
                    f"=> request.{resume}(await {query});",
                    "CS1503",
                ))
            invalid_operations.append((
                f"{request_type}に生のboolは渡せない",
                f"public static 閲覧判定 Run({request_type} request) => request.{resume}(true);",
                "CS1503",
            ))

        for name, source, diagnostic in invalid_operations:
            extra = target / "不正な操作.cs"
            extra.write_text(f"using Demo.Domain; public static class Probe {{ {source} }}")
            result = build()
            if result.returncode == 0 or f"error {diagnostic}" not in result.stdout:
                sys.exit(f"FAIL: {name}\n{result.stdout}{result.stderr}")
            extra.unlink()
            print(f"PASS [{model}]: {name}", flush=True)


for implementation in ["StateMachine", "Continuation"]:
    for variant, model, diagnostic in [
        ("Immutable", "不変版", "CS0200"),
        ("Mutable", "可変版", "CS0272"),
    ]:
        check(implementation, variant, model, diagnostic)
