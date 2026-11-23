using Demo.Api;
using Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Demo.Tests;

public sealed class 閲覧時のSQL発行数テスト
{
    [Theory]
    [InlineData("作者は追加取得しない", "author", 0, null)]
    [InlineData("非フォロワーはフォロー確認で打ち切る", "guest", 1, 閲覧拒否理由.フォローが必要)]
    [InlineData("VIPは購入確認を省略する", "vip", 2, null)]
    [InlineData("購入者は購入まで確認する", "buyer", 3, null)]
    [InlineData("未購入者は購入まで確認して拒否する", "follower", 3, 閲覧拒否理由.購入が必要)]
    public async Task 判定に必要な分だけ実SQLを発行する(
        string caseName,
        string viewer,
        int expectedQueries,
        閲覧拒否理由? expectedReason
    )
    {
        await using var database = await テスト用データベース.作成する();
        await using var db = database.CreateDbContext();
        await 初期データ.投入する(db);
        var story = await db
            .ストーリー一覧.AsNoTracking()
            .SingleAsync(x => x.ID == new ストーリーID("followers-paid"));
        var service = new 閲覧判定サービス(new EfCore閲覧事実取得(database));
        database.計測を開始する();

        var result = await service.判定する(story, new 利用者ID(viewer), default);

        Assert.True(database.クエリ数 == expectedQueries, caseName);
        Assert.Equal(expectedQueries, result.事実取得回数);
        Assert.Equal(expectedReason is null, result.許可);
        Assert.Equal(expectedReason, result.理由);
    }
}
