using Demo.Api;
using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧統合テスト
{
    // 期待値は業務仕様から明示する。本体の条件式で期待値を計算しない。
    [Theory]
    [InlineData("無料公開は誰でも読める", "全体公開", "無料", false, false, false, null)]
    [InlineData("無料限定はフォロワーが読める", "フォロワー限定", "無料", true, false, false, null)]
    [InlineData(
        "無料限定は非フォロワーを拒否する",
        "フォロワー限定",
        "無料",
        false,
        false,
        false,
        閲覧拒否理由.フォローが必要
    )]
    [InlineData("有料公開はVIPが読める", "全体公開", "有料", false, true, false, null)]
    [InlineData("有料公開は購入者が読める", "全体公開", "有料", false, false, true, null)]
    [InlineData(
        "有料公開は未購入の一般会員を拒否する",
        "全体公開",
        "有料",
        false,
        false,
        false,
        閲覧拒否理由.購入が必要
    )]
    [InlineData(
        "有料限定はフォロー済みVIPが読める",
        "フォロワー限定",
        "有料",
        true,
        true,
        false,
        null
    )]
    [InlineData(
        "有料限定はフォロー済み購入者が読める",
        "フォロワー限定",
        "有料",
        true,
        false,
        true,
        null
    )]
    [InlineData(
        "有料限定はフォロー済みでも未購入なら拒否する",
        "フォロワー限定",
        "有料",
        true,
        false,
        false,
        閲覧拒否理由.購入が必要
    )]
    [InlineData(
        "非フォロワーはVIPでも限定作品を読めない",
        "フォロワー限定",
        "有料",
        false,
        true,
        false,
        閲覧拒否理由.フォローが必要
    )]
    [InlineData(
        "非フォロワーは購入済みでも限定作品を読めない",
        "フォロワー限定",
        "有料",
        false,
        false,
        true,
        閲覧拒否理由.フォローが必要
    )]
    public async Task 保存された事実に基づいて閲覧可否を判定する(
        string caseName,
        string visibility,
        string price,
        bool isFollowing,
        bool isVip,
        bool hasPurchased,
        閲覧拒否理由? expectedReason
    )
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            境界値.公開範囲を読む(visibility)!,
            境界値.料金を読む(price)!
        );
        await database.保存する(story);
        if (isFollowing)
            await database.保存する(new フォロー { 閲覧者 = "reader", 作者 = "author" });
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = isVip });
        if (hasPurchased)
            await database.購入を保存する("reader", "story");
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可 == (expectedReason is null), caseName);
        Assert.Equal(expectedReason, result.理由);
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

    [Theory]
    [InlineData("作者は追加取得しない", "author", 0)]
    [InlineData("非フォロワーはフォロー確認で打ち切る", "guest", 1)]
    [InlineData("VIPは購入確認を省略する", "vip", 2)]
    [InlineData("一般会員は購入まで確認する", "buyer", 3)]
    public async Task 判定に必要な分だけ実SQLを発行する(
        string caseName,
        string viewer,
        int expectedQueries
    )
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        await database.保存する(
            story,
            new フォロー { 閲覧者 = "vip", 作者 = "author" },
            new フォロー { 閲覧者 = "buyer", 作者 = "author" },
            new 会員 { 閲覧者 = "vip", VIPである = true }
        );
        await database.購入を保存する("buyer", "story");
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID(viewer), default);

        Assert.True(database.クエリ数 == expectedQueries, caseName);
        Assert.Equal(expectedQueries, result.事実取得回数);
    }

    [Fact]
    public async Task 全体公開の無料作品は実SQLを発行せずに許可される()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        await database.保存する(story);
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可);
        Assert.Equal(0, database.クエリ数);
        Assert.Equal(0, result.事実取得回数);
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
