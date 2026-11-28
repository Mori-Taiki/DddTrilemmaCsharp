using Demo.Api;
using Microsoft.EntityFrameworkCore;

namespace Demo.Tests;

public sealed class 初期データテスト
{
    [Fact]
    public async Task 初期データを再投入しても重複せず保存済みの変更を上書きしない()
    {
        await using var database = await テスト用データベース.作成する();
        await using (var db = database.CreateDbContext())
        {
            await 初期データ.投入する(db);
            var member = await db.会員一覧.SingleAsync(x => x.利用者ID == "vip");
            member.VIPである = false;
            db.購入履歴.Remove(await db.購入履歴.FirstAsync());
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext())
            await 初期データ.投入する(db);

        await using var verification = database.CreateDbContext();
        Assert.Equal(4, await verification.ストーリー一覧.CountAsync());
        Assert.Equal(3, await verification.フォロー関係.CountAsync());
        Assert.Equal(1, await verification.会員一覧.CountAsync());
        Assert.Equal(1, await verification.購入履歴.CountAsync());
        Assert.False((await verification.会員一覧.SingleAsync()).VIPである);
    }
}
