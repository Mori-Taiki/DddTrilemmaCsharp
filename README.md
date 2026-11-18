# DDD Decision パターン — 動く SQLite API

[元記事](https://github.com/Mori-Taiki/MyBlog/blob/main/articles/ddd-trilemma-csharp-union-efcore.md)の Query / Command を、.NET 11・C# 15・EF Core 11・SQLite で実装した実験用アプリです。`closed record` をそのまま使い、CS8509 をエラー化しています。

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

- `Domain/ストーリー.cs`: POCOエンティティ、純粋な判定、再開メソッド、公開範囲変更。EF/HTTPへの参照なし。
- `Api/閲覧判定サービス.cs`: 必要とされた事実を取得して判定を再開。業務分岐はドメイン側。
- `Api/Database.cs`: DbContext、Fluent API、事実取得ポートのEF実装、初期データ。
- `Api/Program.cs`: DIとHTTP境界。Queryはストーリー取得→判定ループ、Commandは追跡付き取得→ドメイン変更→成功時だけSaveChanges。

Queryは各事実取得内でDbContextを生成・破棄します。CommandはリクエストのDbContextがUnit of Workです。ストーリーを永続化モデルとしても使い、private setterをFluent API経由で復元します。元記事のコンストラクタの自己代入になる箇所は採用せず、ファクトリで正しく初期化しています。

## テスト

```bash
./dotnet run --project Tests
# API起動中に実行（初期データの公開範囲を前提）
python Tests/http_smoke.py
```

Testsは外部テストフレームワーク不要の検証用コンソールです。失敗時は非ゼロで終了します。64条件の組み合わせをfakeと実SQLiteインメモリに流し、結果・拒否理由・経路を比較します。fakeで取得回数を確認し、POCO復元、拒否時の不変性、保存後再取得、技術例外の伝播を含め134 assertionを検証します。インメモリDBは接続を開いたまま維持します。

HTTPテストは入力検証、404、判定の打ち切り、公開範囲変更と再取得を確認し、最後に公開範囲を元に戻します。

## 実験用としての範囲

- `viewer` / `actor` は動作を切り替える入力で、認証ではありません。本番では認証済み主体から取得します。
- DBスキーマは `EnsureCreated` で初期化します。運用時のスキーマ変更にはマイグレーションが必要です。
- Queryは単件判定用。複数の事実取得を同じスナップショットで読む保証はありません。一覧のN+1対策、同時更新の競合検知は未実装です。
- SQLiteにSQL Serverのrowversion相当を仮定していません。厳密な同時実行制御を加える場合はSQLite向けのトークン設計が必要です。
- ファイルDBはこのワークスペース内に保存されます。外部公開・常時稼働は設定していません。

## この環境での確認結果

.NET SDK 11.0.100 / EF Core SQLite 11.0.0。ビルドは警告・エラー0件。134 assertionとHTTPテストが成功。別の最小プロジェクトでclosed階層のケースを1つ漏らし、CS8509が実際にビルドエラーになることも確認しました。

## 日本語によるモデリング

記事の業務語彙をそのまま識別子に使います。`閲覧.開始(story, viewer)` が `フォロー確認が必要` などを返し、取得した事実を `結果(bool)` に渡して再開します。公開範囲の変更も `story.公開範囲を変える(newVisibility, actor)` と書き、`変更できた` / `変更拒否` を返します。拒否理由は `拒否理由.フォローが必要` などの日本語の列挙型です。

HTTPのパス・JSONキーは英語を維持し、判定経路と拒否理由は日本語です。公開範囲入力は `全体公開` / `フォロワー限定` です。DBのテーブル・列・列挙値も日本語のモデルに従います。英語版DBへの互換処理はありません。以前のDBを使っている場合は、別の `--DatabasePath` を指定して新規作成してください。ドメインにJSON/EF属性はありません。

### 命名規則

- 型・メソッド・プロパティ・列挙値には日本語の業務語彙を使います。
- ローカル変数・通常の仮引数・フィールドは英語の camelCase にします（`story`、`viewer`、`actor`、`decision` など）。
- record の位置引数は公開プロパティを生成するため、日本語にします。通常の class のプライマリコンストラクタ引数は英語です。
- `AppDb` など技術的な型名、フレームワークで定義されたAPI名、HTTP/JSONのキーは既存の表記を使います。

```csharp
public sealed record フォロー確認が必要(ストーリー 対象, string 閲覧者) : 閲覧判定
{
    public 閲覧判定 結果(bool isFollowing)
        => isFollowing
            ? 閲覧.有料かどうか(対象, 閲覧者)
            : new 拒否(拒否理由.フォローが必要);
}
```

### コード整形

CSharpier 1.4.0 と `.editorconfig` で改行・インデントを揃えています。
長い引数リストやクエリは折り返し、処理のまとまりの間には空行を入れます。

```bash
dotnet tool restore
dotnet csharpier format .
dotnet csharpier check .
```
