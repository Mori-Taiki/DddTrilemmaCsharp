using Demo.Api;
using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧事実取得テスト
{
    [Fact]
    public async Task 作者本人は全体公開の無料作品を追加取得なしで読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "author", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("author"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(0, database.クエリ数);
        Assert.Equal(0, result.事実取得回数);
    }

    [Fact]
    public async Task 作者本人は全体公開の有料作品を追加取得なしで読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "author", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("author"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(0, database.クエリ数);
        Assert.Equal(0, result.事実取得回数);
    }

    [Fact]
    public async Task 作者本人はフォロワー限定の無料作品を追加取得なしで読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "author", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("author"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(0, database.クエリ数);
        Assert.Equal(0, result.事実取得回数);
    }

    [Fact]
    public async Task 作者本人はフォロワー限定の有料作品を追加取得なしで読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "author", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("author"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(0, database.クエリ数);
        Assert.Equal(0, result.事実取得回数);
    }

    [Fact]
    public async Task 無料公開は作者以外も追加取得なしで読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(0, database.クエリ数);
        Assert.Equal(0, result.事実取得回数);
    }

    [Fact]
    public async Task 無料限定はフォロワーが読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        await database.保存する(story);
        await database.保存する(new フォロー { 閲覧者 = "reader", 作者 = "author" });
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(1, database.クエリ数);
        Assert.Equal(1, result.事実取得回数);
    }

    [Fact]
    public async Task 無料限定は非フォロワーを拒否する()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.False(result.許可);
        Assert.Equal(閲覧拒否理由.フォローが必要, result.理由);
        Assert.Equal(1, database.クエリ数);
        Assert.Equal(1, result.事実取得回数);
    }

    [Fact]
    public async Task 有料公開はVIPなら購入確認なしで読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = true });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(1, database.クエリ数);
        Assert.Equal(1, result.事実取得回数);
    }

    [Fact]
    public async Task 有料公開は一般会員も購入済みなら読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        await database.購入を保存する("reader", "story");
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(2, database.クエリ数);
        Assert.Equal(2, result.事実取得回数);
    }

    [Fact]
    public async Task 有料公開は未購入の一般会員を拒否する()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.False(result.許可);
        Assert.Equal(閲覧拒否理由.購入が必要, result.理由);
        Assert.Equal(2, database.クエリ数);
        Assert.Equal(2, result.事実取得回数);
    }

    [Fact]
    public async Task 有料限定はフォロー済みVIPなら購入確認なしで読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        await database.保存する(new フォロー { 閲覧者 = "reader", 作者 = "author" });
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = true });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(2, database.クエリ数);
        Assert.Equal(2, result.事実取得回数);
    }

    [Fact]
    public async Task 有料限定はフォロー済み購入者が読める()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        await database.保存する(new フォロー { 閲覧者 = "reader", 作者 = "author" });
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        await database.購入を保存する("reader", "story");
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.True(result.許可);
        Assert.Null(result.理由);
        Assert.Equal(3, database.クエリ数);
        Assert.Equal(3, result.事実取得回数);
    }

    [Fact]
    public async Task 有料限定はフォロー済みでも未購入の一般会員を拒否する()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        await database.保存する(new フォロー { 閲覧者 = "reader", 作者 = "author" });
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.False(result.許可);
        Assert.Equal(閲覧拒否理由.購入が必要, result.理由);
        Assert.Equal(3, database.クエリ数);
        Assert.Equal(3, result.事実取得回数);
    }

    [Fact]
    public async Task 非フォロワーはVIPでも限定作品を読めない()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = true });
        // 購入は保存しない。
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.False(result.許可);
        Assert.Equal(閲覧拒否理由.フォローが必要, result.理由);
        Assert.Equal(1, database.クエリ数);
        Assert.Equal(1, result.事実取得回数);
    }

    [Fact]
    public async Task 非フォロワーは購入済みでも限定作品を読めない()
    {
        await using var database = await テスト用データベース.作成する();
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        await database.保存する(story);
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        await database.購入を保存する("reader", "story");
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID("reader"), default);

        Assert.False(result.許可);
        Assert.Equal(閲覧拒否理由.フォローが必要, result.理由);
        Assert.Equal(1, database.クエリ数);
        Assert.Equal(1, result.事実取得回数);
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
}
