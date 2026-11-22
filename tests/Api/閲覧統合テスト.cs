using Demo.Api;
using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧統合テスト
{
    [Theory]
    [ClassData(typeof(閲覧仕様一覧))]
    public async Task 共通の仕様どおりに保存された事実から判定する(閲覧仕様 仕様)
    {
        await using var database = await テスト用データベース.作成する();
        var story = 仕様.入力.作品.作成する();
        var reader = 仕様.入力.閲覧者;
        await database.保存する(story);
        if (reader.フォロー == フォロー状況.フォロー済み)
            await database.保存する(new フォロー { 閲覧者 = reader.ID.値, 作者 = story.作者.値 });
        await database.保存する(
            new 会員 { 閲覧者 = reader.ID.値, VIPである = reader.会員 == 会員資格.VIP会員 }
        );
        if (reader.購入 == 購入状況.購入済み)
            await database.購入を保存する(reader.ID.値, story.ID.値);
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, reader.ID, default);

        Assert.Equal(仕様.期待.結果 is 許可, result.許可);
        Assert.Equal(
            仕様.期待.結果 is 拒否 denied ? denied.理由 : (閲覧拒否理由?)null,
            result.理由
        );
        // 現在の実装では、要求した事実一つにつきSELECTを一回実行する。
        Assert.Equal(仕様.期待.取得順序.Length, database.クエリ数);
        Assert.Equal(仕様.期待.取得順序.Length, result.事実取得回数);
    }

    [Theory]
    [InlineData("story", true)]
    [InlineData("other", false)]
    public async Task 対象作品だけの購入と別作品だけの購入を区別する(
        string purchasedStoryId,
        bool expectedAllowed
    )
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var other = new ストーリー(
            new ストーリーID("other"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        await database.保存する(story, other);
        // 両作品を購入したデータでは、作品IDの比較を反転しても検出できない。
        await database.購入を保存する("reader", purchasedStoryId);
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.Equal(expectedAllowed, result.許可);
        Assert.Equal(expectedAllowed ? (閲覧拒否理由?)null : 閲覧拒否理由.購入が必要, result.理由);
    }

    [Fact]
    public async Task 他人や別作品の事実は閲覧許可に流用されない()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var other = new ストーリー(
            new ストーリーID("other"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        await database.保存する(story, other);
        await database.保存する(new 会員 { 閲覧者 = "someone-else", VIPである = true });
        await database.購入を保存する("someone-else", "story");
        await database.購入を保存する("reader", "other");
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.False(result.許可);
        Assert.Equal(閲覧拒否理由.購入が必要, result.理由);
    }

    [Fact]
    public async Task 他の作者へのフォローや他人のフォローは閲覧許可に流用されない()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        await database.保存する(
            story,
            new フォロー { 閲覧者 = "reader", 作者 = "someone-else" },
            new フォロー { 閲覧者 = "someone-else", 作者 = "author" }
        );
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.False(result.許可);
        Assert.Equal(閲覧拒否理由.フォローが必要, result.理由);
    }

    [Fact]
    public async Task 事実取得の障害は業務上の拒否に変換されない()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var service = new 閲覧判定サービス(new 障害を起こす事実取得());

        await Assert.ThrowsAsync<IOException>(() =>
            service.判定する(story, new 利用者ID("reader"), default)
        );
    }

    [Theory]
    [InlineData("全体公開", "有料")]
    [InlineData("フォロワー限定", "無料")]
    public async Task キャンセル済みなら事実取得を開始しない(string visibility, string price)
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            境界値.公開範囲を読む(visibility)!,
            境界値.料金を読む(price)!
        );
        // 呼び出されるとIOExceptionになる取得先でも、先にキャンセルされる。
        var service = new 閲覧判定サービス(new 障害を起こす事実取得());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.判定する(story, new 利用者ID("reader"), cancellation.Token)
        );

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    // 障害・キャンセルの経路を安定して再現する。呼び出し順序は検証しない。
    private sealed class 障害を起こす事実取得 : 閲覧事実取得
    {
        public Task<フォロー確認結果> フォローしているか(
            利用者ID viewer,
            利用者ID author,
            CancellationToken ct
        ) => throw new IOException();

        public Task<会員確認結果> VIPか(利用者ID viewer, CancellationToken ct) =>
            throw new IOException();

        public Task<購入確認結果> 購入済みか(
            利用者ID viewer,
            ストーリーID storyId,
            CancellationToken ct
        ) => throw new IOException();
    }
}
