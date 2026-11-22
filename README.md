# DDDトリレンマをC#の型で扱う

「この作品を読めるか」という判断を、DBに依存しないドメインモデルで表すサンプルです。C# 15 / .NET 11、ASP.NET Core Minimal API、EF Core、SQLiteで、閲覧判定と公開範囲変更を動かせます。

## 閲覧ルールを書こうとすると、DBが必要になる

閲覧ルールは「全体公開の無料作品は誰でも読める」「フォロワー限定ならフォローが必要」「有料ならさらにVIPか購入済みであることが必要」です。作者本人はいつでも読めます。

フォロー・会員・購入の情報はDBにありますが、作者本人や無料公開なら調べる必要がありません。**判断してみないと、次に取得すべき情報が決まらない**のが難しいところです。

| 書き方 | 犠牲になるもの |
|---|---|
| ドメインが必要に応じてDBを呼ぶ | **純粋性**：入力した値だけでは結果が決まらなくなる |
| 呼び出し側で全情報を取得してから渡す | **性能**：判断に使わない情報も取得する |
| 呼び出し側で取得の要否を判断する | **完全性**：業務ルールがドメインの外へ分散する |

この3つの要求を同時に満たしにくい、という問題が[DDDトリレンマ](https://enterprisecraftsmanship.com/posts/domain-model-purity-completeness/)です。

## 必要な事実を返して、判定を再開する

そこで、ドメインが「答え」だけでなく「答えを出すために必要な事実」も返せるようにします。着想は、[GoによるDecisionパターンの実装記事](https://zenn.dev/tellernovel_inc/articles/0193eb68cabb6e)の、**「判断するために何が要るか」を型で返す**という考え方です。

例えば、フォロワー限定の有料作品を読もうとすると、次のように進みます。

```text
ドメイン       → フォロー確認が必要
アプリケーション → DBから取得し、「フォローしている」を渡す
ドメイン       → 会員確認が必要
アプリケーション → DBから取得し、「VIPである」を渡す
ドメイン       → 許可
```

VIPと分かった時点で許可するため、購入履歴は取得しません。最初に「フォローしていない」と分かれば、そこで拒否します。取得処理を実行するのはアプリケーションですが、取得の要否と許可・拒否はドメインが決めます。

これなら業務判断をドメインに集め、必要なときだけ取得しながら、ドメイン自体は値を受け取って値を返す純粋な処理にできます。「フォローしていない」は業務上の判定結果として返し、DB障害は取得を実行した外側へ例外として伝えます。

## 判定の選択肢を型で閉じる

呼び出し側が「完了」と「取得待ち」を見分け、それぞれのデータを受け取れるよう、判定を次の型で表します。

```csharp
public closed record 閲覧判定;
public sealed record 判定完了(閲覧結果 結果) : 閲覧判定;
public sealed record 事実取得待ち(事実要求 要求) : 閲覧判定;
```

`閲覧結果` は許可か拒否、`事実要求` はフォロー・会員・購入の確認要求です。このように、有限の選択肢とケースごとのデータを表すのが **discriminated union（判別共用体）** です。

購入確認を新たに追加したとき、アプリケーション側にも取得処理が必要になります。その追加漏れをコンパイル時に見つけるため、C# 15の `closed record` を使っています。直接の派生を同じアセンブリに制限することで、コンパイラがケース集合を把握できます。末端は `sealed` にして閉じます。

全ケースを列挙したswitch式に処理漏れがあれば `CS8509` が出ます。本実装はこれをビルドエラーにしています。`_` を置くと将来追加されるケースまでそこで処理されるため、ケース追加を検出したい分岐には置きません。公開範囲・料金区分にも同じ仕組みを使います。

C# 15の `union` 宣言との違いは、ケースの表現と初期値の扱いです。

| | `union` 宣言 | `closed record` |
|---|---|---|
| ケースの表現 | 共通基底型のない型も束ねる値型 | 共通基底型から派生する参照型の階層 |
| 初期値 | `default` で内部値がnullの空状態を持つ | 基底は暗黙にabstract。各ケースを生成して使う |
| このモデルでの扱い | 業務ケースに加え、空状態への対応が必要 | 参照のnullを境界で拒否し、内部では業務ケースを扱う |

どちらも網羅性検査の対象ですが、今回はケースをすべて自分で定義できるため、**業務ケース以外の空状態を増やさないclosed階層の方が、網羅性を保ちやすい**と考えました。参照型のnullにはnullable解析と境界での検証を使います。この選択は[C# 15のclosed recordとJSONの検証記事](https://zenn.dev/jtechjapan_pub/articles/csharp15-closed-record-json)も参考にしています。

判定結果に加え、入力も型で区別します。作品を示す `ストーリーID` と、人を示す `利用者ID` は別の型です。作者・閲覧者・実行者には共通の `利用者ID` を使い、空のIDや未知の公開範囲を入口で拒否します。判定の途中では、検証済みの値を扱えます。

取得結果の取り違えを防ぐため、[事実の種類ごとに型を分け](src/StateMachine/Domain/閲覧の事実.cs)、取得から判定の再開まで保ちます。対象の利用者・作品や値の正しさは、クエリとテストで確かめます。

## 二つの継続表現を比較する

`Api` と `Domain` を実装方式ごとにまとめています。両方とも同じ閲覧・公開範囲変更APIを提供し、それぞれ `StoryModel=Immutable` / `Mutable` を選べます。

```text
src/
  StateMachine/     # 要求型のメソッドで判断を再開する既存版
    Api/
    Domain/
  Continuation/     # 要求と一緒にFuncで継続を返す版
    Api/
    Domain/
```

| 比較する場所 | StateMachine | Continuation |
|---|---|---|
| 要求と業務判断 | [要求型の `結果(...)` メソッド](src/StateMachine/Domain/閲覧.cs) | [型付きの `Func<回答型, 閲覧判定>`](src/Continuation/Domain/閲覧.cs) |
| 事実取得と再開 | [`結果(...)` を呼ぶ](src/StateMachine/Api/閲覧判定サービス.cs) | [`続ける(...)` に回答を渡す](src/Continuation/Api/閲覧判定サービス.cs) |
| HTTPのポート | 5080 | 5081 |

継続版では、例えば会員確認を次の値で表します。

```csharp
public sealed record 会員確認が必要(
    利用者ID 閲覧者,
    Func<会員確認結果, 閲覧判定> 続ける
) : 事実要求;
```

要求型には問い合わせに使うIDと継続を置きます。VIPなら許可、一般会員なら購入確認へ進むという判断は、`閲覧.開始` が組み立てるラムダにあります。解釈器はDBから取得した事実を `続ける` に渡します。同じ種類の要求に、呼び出し元ごとに異なる続きを渡せます。

継続は判定開始時の作品ID・料金などの不変な値を捕捉します。`Mutable` でもストーリー本体を捕捉しません。これはプロセス内の値の扱いであり、複数回のDB読み取りのスナップショット分離を保証するものではありません。C#の `Func` は純粋性を強制しないため、継続の中にはI/Oや共有状態の変更を入れません。関数を含む途中状態は、そのままJSON保存したり、recordの等価性で振る舞いを比較したりする用途には向きません。

[継続のテスト](tests/Continuation/継続テスト.cs)ではI/Oやモックを使わず、要求のIDと回答後の状態を確認します。同じ継続に違う回答を渡して分岐を調べ、複数の判定を交互に再開しても値が混ざらないことも検証します。この実装は結果型を閲覧判定に固定したもので、汎用のFree/Freer Monadの合成APIは実装していません。

## 状態変更を値で返すか、エンティティ自身を変えるか

ここまでの閲覧判定は、事実を読んで結果を返す **Query** です。次に、公開範囲を変える **Command** を考えます。このリポジトリでは、同じ業務ルールを2通りのモデルで比較できます。

不変版の `ストーリー` は、`公開範囲を変える` で元の状態を書き換えず、`変更できた(変更後)` または `変更拒否(理由)` を返します。閲覧判定が途中で保持するストーリーも不変です。業務の状態と遷移を型・値・純粋関数で表す、**関数型ドメインモデリング**の実装です。

EF Coreが追跡するのは別の可変な `ストーリー行` で、成功時だけ変更後の状態を適用します。不変性を保つために、ドメインと永続化モデルの間の変換を持ちます。

```csharp
var result = row.ドメインに戻す().公開範囲を変える(newVisibility, actor);
if (result is 変更できた changed)
{
    row.適用する(changed.変更後);
    await db.SaveChangesAsync(ct);
}
```

一方、**EF Coreの変更追跡をそのまま使う**方法もあります。[Microsoftの.NET Blog](https://devblogs.microsoft.com/dotnet/announcing-entity-framework-core-1-1/)では、フィールドのマッピングによって読み取り専用プロパティやメソッド経由のアクセスに対応する機能が紹介されています。[Microsoft掲載の「DDD-Friendlier EF Core 2.0」](https://learn.microsoft.com/en-us/archive/msdn-magazine/2017/september/data-points-ddd-friendlier-ef-core-2-0)では、カプセル化したドメインを直接マッピングできるなら、別の永続化モデルを作らなくてよいという考え方が説明されています。

これに沿う可変版では、`ストーリー` を `class` にし、業務メソッドの中で自身の状態を変えます。プロパティは `private set` なので、呼び出し側からルールを飛ばして公開範囲を書き換えることはできません。ドメインにはEF Coreへの参照やDBアクセスを置かず、独自IDやclosed recordの列への変換はFluent APIに置きます。

```csharp
var story = await db.ストーリー一覧.SingleAsync(x => x.ID == storyId, ct);
var result = story.公開範囲を変える(newVisibility, actor);
if (result is 変更できた)
    await db.SaveChangesAsync(ct);
```

[公式の変更追跡ドキュメント](https://learn.microsoft.com/en-us/ef/core/change-tracking/#tracking-from-queries)が説明する通り、同じDbContextで取得・変更・保存することで、EF Coreが変更を検出します。`Update` や行へのコピーは不要です。DbContextが保存の単位（Unit of Work）を担い、このサンプルではDbSetを包む汎用Repositoryも設けません。可変版のCommandは純粋関数ではありませんが、業務判断をドメインに集め、I/Oを外に置く構成は維持しています。

| 比較する場所 | 不変版 | 可変版 |
|---|---|---|
| 状態と変更 | [新しい状態を返す](src/StateMachine/Domain/Immutable/ストーリー.cs) | [自身を変更する](src/StateMachine/Domain/Mutable/ストーリー.cs) |
| EFの追跡対象 | [ストーリー行](src/StateMachine/Api/Immutable/ストーリー行.cs) | [ストーリーを直接マッピング](src/StateMachine/Api/Mutable/Database.cs) |
| 保存の呼び出し | [適用してから保存](src/StateMachine/Api/Immutable/ストーリーAPI.cs) | [変更後にそのまま保存](src/StateMachine/Api/Mutable/ストーリーAPI.cs) |

各方式の `Domain` に閲覧ルールと値オブジェクト、`Api` に閲覧判定サービスとAPIの起動処理があります。各プロジェクト直下の `Immutable/` と `Mutable/` に各方式の実装を置いています。ビルド時に `StoryModel` で選んだモデルをドメイン・API・テストに組み込みます。既定は `Immutable` です。

Queryはどちらも `AsNoTracking` で読み、「フォローしているか」などの事実取得ポートを通して判定します。可変版でも、取得したオブジェクトを判定中に変更しません。Commandは追跡して変更を保存する、Queryは読んで結果を返す、という役割の違いです。

このサンプルは単件判定です。一覧への拡張では、事実を先に一括取得する方法や、判定途中の要求をまとめて取得する方法を、取得量と往復回数のコストに応じて選べます。

## 日本語の仕様を両実装で共有する

購入確認の処理を書き忘れれば、型の網羅性検査で気づけます。一方、その処理に「未購入でも許可する」と書いてしまう間違いは、期待する業務結果との照合で見つけます。そのため本実装では、「有料作品の一般会員は購入済みの場合だけ閲覧できる」といった具体的な仕様を、日本語のテストケースにしています。

[閲覧仕様一覧](tests/Specifications/閲覧仕様一覧.cs)に15件の入力と期待値をまとめ、StateMachineとContinuationで共有しています。[仕様を表す型](tests/Specifications/閲覧仕様.cs)を使うと、booleanの並びではなく、作品・閲覧者の条件と期待する振る舞いとして読めます。次の例では、条件や期待値の名前を `using static` で取り込んでいます。

```csharp
new 閲覧仕様(
    "有料限定はフォロー済みVIPなら購入確認なしで読める",
    入力: new(フォロワー限定の有料作品, new(作者以外, フォロー済み, VIP会員, 未購入)),
    期待: 許可される(フォロー確認, 会員確認)
)
```

15件の代表例では、期待する許可・拒否理由と、取得する事実の順序を明記します。[共通のドメインテスト](tests/Domain/閲覧ドメインテスト.cs)はこの仕様で要求順序・最終結果を確認します。実装ごとの[StateMachine用の実行処理](tests/StateMachine/閲覧の実行.cs)と[Continuation用の実行処理](tests/Continuation/閲覧の実行.cs)は、要求先のIDを確認して `結果(...)` / `続ける(...)` に回答し、観測した結果と取得順序を返します。実行処理には期待値を渡さず、I/Oやモックも使いません。

実SQLiteの統合テストも同じ入力からデータを保存し、同じ期待値で結果と追加クエリ数を確認します。現在は一つの事実要求につき一回のSELECTを行うため、取得順序の長さが期待するクエリ数になります。各実行で新しいストーリーを作るので、可変モデルでも仕様ケース同士でエンティティを共有しません。他人・別作品のデータの分離や、障害・キャンセルは別のテストで検証します。

さらに、[性質テスト](tests/Domain/閲覧の性質テスト.cs)では個々の期待値を並べず、次の契約を両実装で共有します。

- 許可されるのは、作者本人か、公開範囲と料金の両条件を満たす場合に限る。
- 拒否理由は満たさない条件を示し、フォロー不足を購入不足より優先する。
- 必要な事実だけを一度ずつ、フォロー・会員・購入の順で取得する。
- 取得しなかった事実を反転させても、結果と取得順序は変わらない。

入力は、公開範囲・料金・作者との関係・フォロー・会員資格・購入状況の6個の二択です。[入力生成](tests/Specifications/閲覧入力の全組合せ.cs)で全64通りを列挙し、4つの性質をそれぞれに適用します。PBTの考え方を有限な入力空間に全数適用しており、ランダム生成や失敗例の縮小を行うライブラリは使っていません。許可条件は入力に対する論理式で表し、取得手順の実装を呼んだり、片方の実装の出力を正解にしたりはしません。失敗時には入力の条件を日本語で表示します。

64通りはこの6条件の組み合わせについての網羅性です。任意のIDや状態変更の操作列まで網羅するものではなく、継続の独立性・可変モデルの状態変更・HTTP・DBの検証はそれぞれのテストで続けます。

この仕様と実装の関係をさらに進める方向として、[SoutherによるDDDトリレンマへのアプローチ](https://zenn.dev/kawasima/articles/souther-ddd-trilemma)があります。Decisionパターンでは途中状態を手で型にする必要がありますが、Southerは振る舞いの契約と合成可能性に注目します。入出力の関係を事後条件で表し、仕様例の不足を機械的に調べ、同じ仕様例で外部実装も検証します。

現在は代表例と有限な入力空間での性質を併用しています。今後、IDや操作列を含む入力生成へ広げる際には、生成範囲と失敗例の縮小方法も設計できます。

## 動かして確認する

[GitHub Codespacesで開く](https://codespaces.new/Mori-Taiki/DddTrilemmaCsharp)と、SDKの準備・依存関係の復元・ビルドが自動で行われます。ローカルでは.NET SDK **11.0.100**を導入してcloneしてください。リポジトリ直下で起動します。

```bash
# 既存の状態機械版（5080）
dotnet run --project src/StateMachine/Api

# 別のターミナルで継続関数版（5081）
dotnet run --project src/Continuation/Api

# 可変モデルで試す場合（同じポートのAPIは先に停止）
dotnet run --project src/Continuation/Api -p:StoryModel=Mutable
```

StateMachineは `http://localhost:5080`、Continuationは `http://localhost:5081` です。Codespacesでは **Ports** から対応するポートを開きます。VS Codeでは「APIを起動」で実装方式とモデルを選べます。

SQLite DBはそれぞれの `Api/app.db` に作られ、StateMachineとContinuationで独立しています。同じ方式の不変／可変モデルはDBを共有します。以前の `src/Api/app.db` はGit管理外のため移動されません。既存のDBや共通のDBを使うには、起動コマンドの末尾に `-- --DatabasePath /path/to/app.db` を指定してください。

```bash
curl 'http://localhost:5080/stories/followers-paid/view?viewer=vip'
curl -X PUT http://localhost:5080/stories/public-free/visibility \
  -H 'Content-Type: application/json' \
  -d '{"actor":"author","visibility":"フォロワー限定"}'
# 両実装を不変モデルでテスト
dotnet test DddDemo.sln
# 両実装を可変モデルでテスト
dotnet test DddDemo.sln -p:StoryModel=Mutable
# 継続関数版だけをテスト
dotnet test tests/Continuation/Continuation.Tests.csproj
```

継続版を試すときはcurlのポートを `5081` に変えてください。

`viewer` を `guest`・`follower`・`vip`・`buyer`・`author` に変えると、結果・判定経路・追加クエリ数を比較できます。追加クエリ数には、最初のストーリー取得は含みません。利用者指定はデモ用で、認証ではありません。

閲覧の代表例・性質・入力検証の共通テストは [tests/Domain](tests/Domain)、共通の入力・期待値・入力生成は [tests/Specifications](tests/Specifications) にあります。[tests/StateMachine](tests/StateMachine) と [tests/Continuation](tests/Continuation) に実装ごとの実行処理を置きます。HTTP・実SQLiteの [tests/Api](tests/Api)、各モデルの [tests/Immutable](tests/Immutable) と [tests/Mutable](tests/Mutable) も二つのテストプロジェクトで共用します。可変版では、変更の保存・拒否時の不変性・追跡しない場合の挙動も実SQLiteで検証します。型による保証は `python3 scripts/check_exhaustiveness.py` で4通りすべてを確認できます。
