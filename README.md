# DDDトリレンマをC#の型で扱う

「この人は、この作品を読めるか？」を題材に、**業務ルールをドメインに集めたまま、必要になったデータだけをDBから取得する**サンプルです。C# 15 / .NET 11、ASP.NET Core Minimal API、EF Core、SQLiteを使います。

## 閲覧判定のどこが難しいか

作者は自分の作品をいつでも読めます。それ以外の人が読むには、公開範囲と料金の両方の条件を満たす必要があります。フォロワー限定の作品には作者へのフォローが必要で、有料作品にはVIP会員資格かその作品の購入実績が必要です。

たとえば、**フォロワー限定の有料作品**を考えます。作者本人なら、作品に記録された作者IDを見るだけで許可できます。作者以外ならまずフォローを確認します。フォロー済みでも、VIPならそこで許可でき、一般会員なら購入履歴まで確認する必要があります。**どのデータを読むべきか自体が、業務ルールの一部**です。

| 素直な実装 | この題材で起きること |
|---|---|
| ドメインが必要な時点でDBを呼ぶ | 判定にI/Oが入り、引数だけで結果を決める純粋性を失う |
| 呼び出し側がすべての事実を先に取得する | 作者本人の購入履歴まで読むなど、使わないデータの取得コストが生じる |
| 呼び出し側が必要な事実を選んで取得する | 「作者なら確認を省く」などのルールが外に出て、ドメインだけでは判断の全体を読めなくなる |

純粋性・性能・ドメインモデルの完全性の間に生じる、この緊張関係がDDDトリレンマです。[Domain model purity and completeness](https://enterprisecraftsmanship.com/posts/domain-model-purity-completeness/)

## 「次に知りたい事実」を返す

この実装では、ドメインが一度の呼び出しで最終結果まで出す必要はありません。判定を進めると、次のどちらかの値が返ります。

```csharp
public closed record 閲覧判定;
public sealed record 判定完了(閲覧結果 結果) : 閲覧判定;
public sealed record 事実取得待ち(事実要求 要求) : 閲覧判定;
```

先ほどの作品をフォロー済みのVIPが読む場合、ドメインはまず `フォロー確認が必要` を返します。API側の閲覧判定サービスがDBを読み、「フォローしている」と回答すると、次は `会員確認が必要` が返ります。「VIPである」と回答すると `判定完了(許可)` です。購入履歴を読む要求は出ません。

**ドメインは何を知れば判断できるかを決め、サービスは要求された事実を取得して回答します。** 「フォローしていない」はドメインが扱う業務上の拒否です。DB障害はサービスから例外として伝播し、HTTP境界でサーバーエラーになります。この分担は、Decisionパターンで途中の要求を値として表す発想に基づいています。[着想元のGo実装](https://zenn.dev/tellernovel_inc/articles/0193eb68cabb6e)

### APIで判定を観察する

ローカルでは.NET SDK 11.0.100を用意し、リポジトリ直下で次を実行します。Codespacesでは環境作成時に復元とビルドが実行されます。[GitHub Codespacesで開く](https://codespaces.new/Mori-Taiki/DddTrilemmaCsharp)

```bash
dotnet run --project src/StateMachine/Api
```

別のターミナルから、先ほどのVIPの例を試せます。

```bash
curl 'http://localhost:5080/stories/followers-paid/view?viewer=vip'
```

初期データで `viewer` だけを変えると、同じ作品でも次の違いが見えます。`trace` には実際に通った要求と最終結果が入り、`factQueries` には追加の事実取得回数が入ります。最初の作品取得はこの回数の外です。

| viewer | 確認する事実（順番どおり） | 結果 | factQueries |
|---|---|---|---:|
| `author` | なし | 許可 | 0 |
| `guest` | フォロー | 拒否（フォローが必要） | 1 |
| `vip` | フォロー → 会員 | 許可 | 2 |
| `follower` | フォロー → 会員 → 購入 | 拒否（購入が必要） | 3 |
| `buyer` | フォロー → 会員 → 購入 | 許可 | 3 |

## 同じ仕様を、二つの軸で比較する

閲覧判定は情報を読んで答えを返す **Query** です。このAPIにはもう一つ、作者が作品の公開範囲を変える **Command** があります。それぞれ別の設計判断を比較できるようにしています。

| 比較する問い | 選び方 | 選択肢 |
|---|---|---|
| Queryで、事実が返った後の「続き」をどう表すか | 起動するプロジェクト | `StateMachine` / `Continuation` |
| Commandで、変更した状態をどう保存するか | ビルドプロパティ `StoryModel` | `Immutable`（既定）/ `Mutable` |

どの組み合わせでも同じHTTP APIと業務ルールを使います。

### Query — 回答を受け取ったら、どこから再開するか

両方式の差は「一般会員だったので購入確認に進む」という分岐を読むと分かります。

| | StateMachine | Continuation |
|---|---|---|
| 分岐を書く場所 | `会員確認が必要` 型の `結果(会員確認結果)` メソッド | `閲覧.開始` が要求を作るときに渡すラムダ |
| 要求が持つもの | 判定を続けるための対象ストーリーと閲覧者 | 問い合わせに使うIDと `Func<会員確認結果, 閲覧判定>` |
| サービスからの再開 | `要求.結果(回答)` | `要求.続ける(回答)` |
| コード | [ドメイン](src/StateMachine/Domain/閲覧.cs)・[サービス](src/StateMachine/Api/閲覧判定サービス.cs) | [ドメイン](src/Continuation/Domain/閲覧.cs)・[サービス](src/Continuation/Api/閲覧判定サービス.cs) |

StateMachineでは、ある要求への回答で何が起きるかを、その要求型からたどれます。Continuationでは、同じ種類の要求にも呼び出し元ごとの続きを渡せるため、一つの閲覧ルールを開始地点から読めます。継続が捕捉するのは判定開始時の作品IDや料金などの値です。可変モデルでも、後から変更されうるストーリー本体を捕捉しません。[同じ継続を異なる回答で再開するテスト](tests/Continuation/継続テスト.cs)

この継続はプロセス内の関数です。純粋性は関数内にI/Oや共有状態の変更を持ち込まない実装に依存します。途中状態のJSON保存や、複数のDB読み取りを同一スナップショットにそろえることまで、関数を保持するだけで実現できるわけではありません。また、この例は単件判定なので、一覧に適用する場合は事実の一括取得とDB往復回数も考慮する必要があります。

### Command — 公開範囲を変えた結果をどう保存するか

公開範囲を変更できるのは作者だけです。同じ公開範囲を指定した場合も変更は拒否されます。成功時に何が変わり、EF Coreが何を追跡するかがモデル間の違いです。

| | Immutable | Mutable |
|---|---|---|
| 業務メソッド | 元のストーリーを保持し、`変更できた(変更後)` で新しい値を返す | メソッド内でストーリー自身の状態を変える。外からの代入は `private set` で制限する |
| 保存 | 追跡中の `ストーリー行` に変更後の値を適用し、`SaveChangesAsync` を呼ぶ | 同じ `DbContext` で取得したストーリーを変更し、`SaveChangesAsync` を呼ぶ |
| コード（StateMachine版） | [ストーリー](src/StateMachine/Domain/Immutable/ストーリー.cs)・[行への変換](src/StateMachine/Api/Immutable/ストーリー行.cs)・[保存](src/StateMachine/Api/Immutable/ストーリーAPI.cs) | [ストーリー](src/StateMachine/Domain/Mutable/ストーリー.cs)・[EFのマッピング](src/StateMachine/Api/Mutable/Database.cs)・[保存](src/StateMachine/Api/Mutable/ストーリーAPI.cs) |

不変版では状態遷移を値から値への関数として扱える代わりに、永続化用の行との変換が必要です。可変版ではドメインのエンティティを直接マッピングし、EF Coreの変更追跡を利用します。どちらもDBアクセスはドメインの外に置き、閲覧時には `AsNoTracking` で作品を読みます。[EF Coreの変更追跡](https://learn.microsoft.com/en-us/ef/core/change-tracking/#tracking-from-queries)・[カプセル化したドメインのマッピング](https://learn.microsoft.com/en-us/archive/msdn-magazine/2017/september/data-points-ddd-friendlier-ef-core-2-0)

継続版と可変モデルの組み合わせは次のコマンドで起動できます。StateMachineは5080、Continuationは5081を使います。VS Codeでは「APIを起動」から方式とモデルを選べます。Codespacesからアクセスする場合は **Ports** で対応するポートを開きます。

```bash
dotnet run --project src/Continuation/Api -p:StoryModel=Mutable
```

次の例は5081側で公開範囲を変更します。変更後、同じ作品を `guest` が閲覧すると、フォローが必要という理由で拒否されます。

```bash
curl -X PUT http://localhost:5081/stories/public-free/visibility \
  -H 'Content-Type: application/json' \
  -d '{"actor":"author","visibility":"フォロワー限定"}'

curl 'http://localhost:5081/stories/public-free/view?viewer=guest'
```

SQLiteの初期データは起動時に投入されます。各方式はそれぞれの `Api/app.db` を使い、同じ方式の不変版と可変版はDBを共有します。以前の `src/Api/app.db` など既存のDBを指定する場合は、起動コマンドの末尾に `-- --DatabasePath /path/to/app.db` を加えます。`viewer` と `actor` はデモ用の利用者指定です。

## 型とテストで何を確かめるか

**購入確認を追加し忘れた場合**と、**購入確認を書いたが未購入者を許可した場合**では、間違いの見つけ方が違います。

### 型で要求と回答を対応させる

`閲覧判定`・`閲覧結果`・`事実要求` は、ケースごとに異なるデータを持つ判別共用体として表現します。C# 15の `closed record` と末端の `sealed record` でケース集合を閉じ、各ケースを列挙するswitch式には網羅性検査を効かせます。`CS8509` をビルドエラーにしているので、要求を追加すると、対応が必要な分岐をコンパイラが知らせます。

C# 15の `union` 宣言は共通の基底型を持たない型も束ねられますが、値型の `default` で内部値がnullになる状態を持ちます。今回はすべてのケースをドメイン側で定義できるので、閉じた参照型の階層を選び、参照のnullは入口で検証します。[closed recordの検証記事](https://zenn.dev/jtechjapan_pub/articles/csharp15-closed-record-json)

フォロー・会員・購入の回答もそれぞれ別の型です。`会員確認が必要` に購入確認の結果を渡す取り違えはコンパイル時に検出できます。一方、同じ型の別人の購入履歴を取得した場合は、クエリの意味をテストで確かめる必要があります。[事実の型](src/StateMachine/Domain/閲覧の事実.cs)・[利用者IDとストーリーID](src/StateMachine/Domain/識別子.cs)

### 共通の仕様で4構成の振る舞いを照合する

冒頭のVIPの例は、[閲覧仕様一覧](tests/Shared/閲覧/仕様/閲覧仕様一覧.cs)にもそのまま登場します。

```csharp
new 閲覧仕様(
    "有料限定はフォロー済みVIPなら購入確認なしで読める",
    入力: new(フォロワー限定の有料作品, new(作者以外, フォロー済み, VIP会員, 未購入)),
    期待: 許可される(フォロー確認, 会員確認)
)
```

| 検証 | 確かめること |
|---|---|
| [ドメインの代表例](tests/Shared/閲覧/閲覧ドメインテスト.cs) | 15件の仕様で、許可・拒否理由と事実の要求順序を照合する。[状態機械版](tests/StateMachine/閲覧の実行.cs)と[継続版](tests/Continuation/閲覧の実行.cs)の実行処理には入力だけを渡し、実際の観測結果を比較する |
| [全64入力に対する性質](tests/Shared/閲覧/閲覧の性質テスト.cs) | 公開範囲・料金・作者との関係・フォロー・会員資格・購入の6個の二択を全列挙する。許可条件、拒否理由の優先順位、事実取得の順序と回数、未取得の事実を変えても結果と順序が変わらないことを検証する |
| [SQLiteとの統合](tests/Shared/閲覧/閲覧判定サービス統合テスト.cs) | 同じ代表例をDB上に用意し、判定結果と実際の追加クエリ数を照合する。別人・別作品の事実も区別する |
| [HTTP](tests/Shared/Http)・[モデル別のテスト](tests/StoryModels) | HTTPの応答と変更後の閲覧結果、不変版の行への適用、可変版の変更追跡と保存を確認する |

64通りで網羅するのは6条件の組み合わせです。IDの取り違え、複数の継続の独立性、[障害・キャンセル](tests/Shared/閲覧/閲覧判定サービス異常系テスト.cs)はそれぞれ別の例で検証します。仕様例と入出力の性質を組み合わせる考えをさらに掘り下げるには、Southerによる契約ベースのアプローチも参考になります。[SoutherによるDDDトリレンマへのアプローチ](https://zenn.dev/kawasima/articles/souther-ddd-trilemma)

### 検証コマンド

リポジトリ直下で実行します。最初の2コマンドで、判定方式とストーリーモデルの全4構成を検証できます。

```bash
# StateMachineとContinuationのテストを両方実行する
dotnet test DddDemo.sln
dotnet test DddDemo.sln -p:StoryModel=Mutable

# 継続版だけを実行する（既定はImmutable）
dotnet test tests/Continuation/Continuation.Tests.csproj

# 一時コピーにケース追加や型の取り違えを導入し、コンパイル時の検出を確認する
python3 scripts/check_exhaustiveness.py
```
