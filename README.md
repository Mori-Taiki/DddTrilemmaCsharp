# DDD Decision パターン — 動く SQLite API

[元記事](https://github.com/Mori-Taiki/MyBlog/blob/main/articles/ddd-trilemma-csharp-union-efcore.md)の Query / Command を、.NET 11・C# 15・EF Core 11・SQLite で実装した実験用アプリです。`closed record` をそのまま使い、CS8509 をエラー化しています。

## GitHub Codespacesで起動

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/Mori-Taiki/DddTrilemmaCsharp)

1. 上のボタン、またはGitHubの **Code → Codespaces → Create codespace on main** を開きます。
2. 初回はコンテナ作成後、依存関係の復元とビルドが自動で実行されます。
3. ターミナルで次のコマンドを実行します（**Tasks: Run Task → APIを起動** でも同じです）。

```bash
dotnet run --project Api -- --urls http://0.0.0.0:5080
```

**Ports** タブの `5080 / DDD API` からブラウザで開きます。転送URLの末尾に `/stories` や `/stories/followers-paid/view?viewer=vip` を付けると結果を確認できます。ターミナルでは通常どおり `curl http://localhost:5080/stories` が使えます。ポートは既定のPrivateのまま利用できます。

```bash
dotnet test Tests/Tests.csproj
python3 scripts/check_exhaustiveness.py
```

.NET SDK 11.0.100・Git・curl・Pythonは開発コンテナに含まれます。SQLiteはNuGetパッケージに同梱されるのでDBサーバーは不要です。`./dotnet` もCodespacesのSDKを利用します。

`Api/app.db` はワークスペース内に保存され、Codespaceの停止・再開でも保持されます。Codespaceを削除すると失われます。既存のCodespaceにこの設定を取り込んだ場合は **Codespaces: Rebuild Container** を実行してください。SDKを更新する際は `global.json` と `.devcontainer/Dockerfile` のバージョンを揃えます。

## 起動

この環境では SDK を `/workspace/.dotnet` に導入済みです。

```bash
cd /workspace/ddd-demo
./dotnet run --project Api -- --urls http://0.0.0.0:5080
```

別のPCでは .NET SDK 11.0.100 を導入し、`./dotnet` を `dotnet` に置き換えてください。SQLiteの別途インストールは不要です。この実行環境ではネットワーク復元・HTTP待受にサンドボックス外の実行許可が必要です。

起動時に `Api/app.db` を作成し、空ならサンプルデータを投入します。`--DatabasePath /absolute/path/demo.db` で保存先を変更できます。再起動しても既存データを維持します。SQLはコンソールに表示されます。

## 試す

```bash
curl http://localhost:5080/stories
curl 'http://localhost:5080/stories/followers-paid/view?viewer=vip'
curl -X PUT http://localhost:5080/stories/public-free/visibility \
  -H 'Content-Type: application/json' \
  -d '{"actor":"author","visibility":"フォロワー限定"}'
```

閲覧判定レスポンス例：

```json
{"allowed":true,"reason":null,"trace":["フォロー確認が必要","会員確認が必要","許可"],"factQueries":2}
```

`factQueries` は事実取得ポートの呼び出し数で、ストーリー取得のSELECTを含みません。現実装では各呼び出しが1回の `EXISTS` クエリです。SQLログと照合できます。拒否でも trace と理由を返します。

| viewer | followers-paid の結果 | 追加クエリ数 |
|---|---|---:|
| author | 許可（作者） | 0 |
| guest | フォローが必要 | 1 |
| follower | 購入が必要 | 3 |
| vip | 許可（フォロー＋VIP） | 2 |
| buyer | 許可（フォロー＋購入） | 3 |

ストーリーは `public-free` / `public-paid` / `followers-free` / `followers-paid` の4件です。無料公開は誰でも追加クエリ0件で許可されます。

HTTP: 許可200、業務拒否403、変更済み409、対象なし404、入力不正400、予期しない技術的失敗500（Problem Details）。

## 構成と処理の流れ

- `Domain/識別子.cs`: 値で比較する `利用者ID` / `ストーリーID`。空・空白・nullを拒否します。
- `Domain/ストーリー.cs`: 不変なストーリーと公開範囲変更。公開範囲・料金区分は `closed record`。
- `Domain/閲覧.cs`: 純粋な閲覧判定と事実要求。EF・HTTP・非同期処理への依存なし。
- `Api/境界値.cs`: HTTP・DBの文字列とドメイン型との変換。
- `Api/ストーリー行.cs`: EFが追跡する可変な永続化モデルと、不変なドメインとの変換。
- `Api/閲覧判定サービス.cs`: 事実要求の実行と判定の再開。
- `Api/Database.cs`: DbContext、Fluent API、事実取得のEF実装、初期データ。
- `Api/Program.cs`: HTTP入力の検証、変更結果の適用、保存。

### オブジェクトの生成

単純な生成にはコンストラクタを使います。`new ストーリー(id, author, visibility, price)` は引数を検証して不変な状態を作り、`new ストーリー行(story)` は永続化用の行を作ります。生成を包むだけだった `投稿する` / `作成する` は設けません。

IDの `作成できる` は外部入力の不正を例外にせず結果で返すために残しています。テストDBの非同期初期化や、複数のケースを返す閲覧判定・境界値の解析も、コンストラクタとは役割が異なるstatic処理です。

### 型で閉じた選択肢と入力境界

公開範囲は `new 公開範囲.全体公開()` / `new 公開範囲.フォロワー限定()`、料金区分は `new 料金区分.無料()` / `new 料金区分.有料()` で表します。名前付きケースに `_` を置かず、網羅的なswitch式を使います。ケースを追加すると、ドメインの分岐や文字列への変換でCS8509がエラーになります。

HTTPの文字列は開いた入力なので、境界の文字列解析には `_ => null` が必要です。未知の入力を有料などの既存ケースへ読み替えず400にします。DBの未知の値もドメインへの復元時に拒否します。

作者・閲覧者・実行者はすべて `利用者ID`、作品の識別子は `ストーリーID` です。暗黙のstring変換は設けていません。IDの有効条件は「null・空文字・空白だけではない」で、文字列のトリミングや大文字小文字の同一視はしません。

### 終端結果と追加事実の要求

```text
閲覧判定
├─ 判定完了(閲覧結果)
│  ├─ 許可
│  └─ 拒否(理由)
└─ 事実取得待ち(事実要求)
   ├─ フォロー確認が必要
   ├─ 会員確認が必要
   └─ 購入確認が必要
```

アプリケーションは外側の判定を網羅的に解釈し、取得待ちなら事実要求だけを別の網羅的switchで実行します。要求には終端結果が混ざらないので、到達しない `許可 => 許可` のような枝は不要です。判定は最大3回の事実取得で完了する現在の遷移に合わせ、非同期で再帰的に解釈します。型の網羅性は終了性そのものを保証するものではありません。

### 不変なドメインからEFの追跡対象への適用

```csharp
var story = row.ドメインに戻す();
var result = story.公開範囲を変える(newVisibility, actor);

if (result is 変更できた changed)
{
    row.適用する(changed.変更後);
    await db.SaveChangesAsync(ct);
}
```

`公開範囲を変える` は元のストーリーを変更せず、`変更できた(変更後)` または `変更拒否(理由)` を返します。プロパティはgetのみで、同じ状態と引数からは同じ結果が得られます。中間の事実要求が保持するストーリーも不変です。

EFが追跡するのは `ストーリー行` です。アプリケーションが成功時だけ変更後の状態を行に適用して保存します。ドメインをEFに合わせて可変にする代わりに、永続化モデルと変換を持つトレードオフを選んでいます。汎用Repositoryは追加していません。Queryの事実取得は引き続き実装内でDbContextを生成・破棄します。

元記事の「可変POCOをEFが直接追跡する」版は[変更前の実装](https://github.com/Mori-Taiki/DddTrilemmaCsharp/tree/0add621)と比較できます。現在のmainは不変モデル版に統一しています。

## テスト

```bash
./dotnet test Tests/Tests.csproj
# 日本語のケース名を個別に表示する
./dotnet test Tests/Tests.csproj --logger "console;verbosity=normal"
# 閲覧の統合テストだけ実行する
./dotnet test Tests/Tests.csproj --filter FullyQualifiedName~閲覧統合テスト
```

xUnitで各ケースを独立実行します。APIの手動起動やPythonは不要です。

| ファイル | 検証する仕様 |
|---|---|
| `Tests/閲覧ドメインテスト.cs` | 作者・無料公開・フォロー・VIP・購入による判定 |
| `Tests/公開範囲変更テスト.cs` | 変更結果とエンティティの状態、拒否時の不変性 |
| `Tests/閲覧統合テスト.cs` | 実SQLiteの事実を使った閲覧可否、他人の事実との分離、追加クエリ数、障害の伝播 |
| `Tests/境界値テスト.cs` | IDの妥当性、不正な保存値の拒否、不変状態から行への明示的適用 |
| `Tests/Apiテスト.cs` | HTTP入力と応答、保存後の状態と次の閲覧判定 |

古典派の方針で、ドメインとサービスは実物を組み合わせ、戻り値と観測できる状態を検証します。正常系では事実取得やDbContextをモックに置き換えず、実SQLiteを使用します。テストの準備・実行・検証は空行で区切り、期待値は仕様に基づいて明記します。本体の条件式をコピーして期待値を計算したり、内部の呼び出し順序や判定経路全体を固定したりしません。

DBは各テストが所有します。閲覧の統合テストは専用接続のSQLiteインメモリ、HTTPテストは専用一時ディレクトリのSQLiteファイルと `WebApplicationFactory` を使い、終了時に破棄します。実行順や起動済みAPIに依存しません。

例外として、記事の性能要件である「不要な事実を取得しない」は専用ケースで実SQLの数を計測します。技術的障害を安定して再現するケースだけ、例外を投げるスタブを使用します。共通化はDBとアプリのライフサイクルに留め、業務上の前提は各ケースに記述します。

## 実験用としての範囲

- `viewer` / `actor` は動作を切り替える入力で、認証ではありません。本番では認証済み主体から取得します。
- DBスキーマは `EnsureCreated` で初期化します。運用時のスキーマ変更にはマイグレーションが必要です。
- Queryは単件判定用。複数の事実取得を同じスナップショットで読む保証はありません。一覧のN+1対策、同時更新の競合検知は未実装です。
- SQLiteにSQL Serverのrowversion相当を仮定していません。厳密な同時実行制御を加える場合はSQLite向けのトークン設計が必要です。
- ファイルDBはこのワークスペース内に保存されます。外部公開・常時稼働は設定していません。

### コンパイル時の保証を検証する

```bash
python scripts/check_exhaustiveness.py ./dotnet
```

一時コピーに公開範囲・料金区分・判定・事実要求・終端結果のケースを追加し、それぞれCS8509でビルドが失敗することを確認します。IDの取り違えとストーリーの直接変更もコンパイル不能であることを確認します。リポジトリ内の実装は書き換えません。別のPCでは `./dotnet` を通常のdotnet実行ファイルのパスに置き換えるか、省略してください。

## この環境での確認結果

.NET SDK 11.0.100 / EF Core SQLite 11.0.0。ビルドは警告・エラー0件。xUnitによるドメイン・SQLite・HTTPテストを実行できます。別の最小プロジェクトでclosed階層のケースを1つ漏らし、CS8509が実際にビルドエラーになることも確認しました。

## 日本語によるモデリング

記事の業務語彙をそのまま識別子に使います。`閲覧.開始(story, viewer)` が `判定完了` または `事実取得待ち` を返し、取得した事実を `結果(bool)` に渡して再開します。公開範囲の変更も `story.公開範囲を変える(newVisibility, actor)` と書き、`変更できた` / `変更拒否` を返します。拒否理由は `閲覧拒否理由.フォローが必要` などの日本語の列挙型です。

HTTPのパス・JSONキーは英語を維持し、判定経路と拒否理由は日本語です。公開範囲入力は `全体公開` / `フォロワー限定` です。DBのテーブル・列・列挙値も日本語のモデルに従います。英語版DBへの互換処理はありません。以前のDBを使っている場合は、別の `--DatabasePath` を指定して新規作成してください。ドメインにJSON/EF属性はありません。

### 命名規則

- 型・メソッド・プロパティ・列挙値には日本語の業務語彙を使います。
- ローカル変数・通常の仮引数・フィールドは英語の camelCase にします（`story`、`viewer`、`actor`、`decision` など）。
- record の位置引数は公開プロパティを生成するため、日本語にします。通常の class のプライマリコンストラクタ引数は英語です。
- `AppDb` など技術的な型名、フレームワークで定義されたAPI名、HTTP/JSONのキーは既存の表記を使います。

```csharp
public sealed record フォロー確認が必要(ストーリー 対象, 利用者ID 閲覧者) : 事実要求
{
    public 閲覧判定 結果(bool isFollowing)
        => isFollowing
            ? 閲覧.有料かどうか(対象, 閲覧者)
            : new 判定完了(new 拒否(閲覧拒否理由.フォローが必要));
}
```

### コード整形

CSharpier 1.4.0 と `.editorconfig` で改行・インデントを揃えています。
長い引数リストやクエリは折り返し、処理のまとまりの間には空行を入れます。`Domain/ストーリー.cs` と `Domain/閲覧.cs` は、短い型宣言をまとめた手動の書式を維持するため `.csharpierignore` で自動整形から除外しています。

```bash
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
```
