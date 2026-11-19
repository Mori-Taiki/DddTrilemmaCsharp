using Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Demo.Tests;

public sealed class 変更追跡テスト
{
    [Fact]
    public async Task 取得したドメインの変更をSaveChangesだけで保存し別のコンテキストから復元できる()
    {
        await using var database = await テスト用データベース.作成する();
        await database.保存する(
            new ストーリー(
                new ストーリーID("story"),
                new 利用者ID("author"),
                new 公開範囲.全体公開(),
                new 料金区分.有料()
            )
        );

        await using (var db = database.CreateDbContext())
        {
            var story = await db.ストーリー一覧.SingleAsync(x => x.ID == new ストーリーID("story"));
            var result = story.公開範囲を変える(
                new 公開範囲.フォロワー限定(),
                new 利用者ID("author")
            );
            Assert.IsType<変更できた>(result);
            await db.SaveChangesAsync();
        }

        await using var verification = database.CreateDbContext();
        var saved = await verification.ストーリー一覧.SingleAsync();
        Assert.Equal(new ストーリーID("story"), saved.ID);
        Assert.Equal(new 利用者ID("author"), saved.作者);
        Assert.Equal(new 料金区分.有料(), saved.料金);
        Assert.Equal(new 公開範囲.フォロワー限定(), saved.公開範囲);
    }

    [Theory]
    [InlineData("作者以外の変更", "reader", "フォロワー限定", 変更拒否理由.作者ではない)]
    [InlineData("同じ公開範囲への変更", "author", "全体公開", 変更拒否理由.既にその公開範囲)]
    public async Task 変更が拒否された後で保存してもDBは変わらない(
        string caseName,
        string actor,
        string visibility,
        変更拒否理由 expectedReason
    )
    {
        await using var database = await テスト用データベース.作成する();
        await database.保存する(
            new ストーリー(
                new ストーリーID("story"),
                new 利用者ID("author"),
                new 公開範囲.全体公開(),
                new 料金区分.無料()
            )
        );

        await using (var db = database.CreateDbContext())
        {
            var story = await db.ストーリー一覧.SingleAsync();
            var result = story.公開範囲を変える(
                Demo.Api.境界値.公開範囲を読む(visibility)!,
                new 利用者ID(actor)
            );
            Assert.True(result == new 変更拒否(expectedReason), caseName);
            Assert.Equal(0, await db.SaveChangesAsync());
        }

        await using var verification = database.CreateDbContext();
        var saved = await verification.ストーリー一覧.SingleAsync();
        Assert.Equal(new 公開範囲.全体公開(), saved.公開範囲);
    }

    [Fact]
    public async Task SaveChangesを呼ばなければドメインを変更してもDBは変わらない()
    {
        await using var database = await テスト用データベース.作成する();
        await database.保存する(
            new ストーリー(
                new ストーリーID("story"),
                new 利用者ID("author"),
                new 公開範囲.全体公開(),
                new 料金区分.無料()
            )
        );

        await using (var db = database.CreateDbContext())
        {
            var story = await db.ストーリー一覧.SingleAsync();
            story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"));
        }

        await using var verification = database.CreateDbContext();
        var saved = await verification.ストーリー一覧.SingleAsync();
        Assert.Equal(new 公開範囲.全体公開(), saved.公開範囲);
    }

    [Fact]
    public async Task 追跡しないQueryで取得したオブジェクトの変更は保存されない()
    {
        await using var database = await テスト用データベース.作成する();
        await database.保存する(
            new ストーリー(
                new ストーリーID("story"),
                new 利用者ID("author"),
                new 公開範囲.全体公開(),
                new 料金区分.無料()
            )
        );

        await using (var db = database.CreateDbContext())
        {
            var story = await db.ストーリー一覧.AsNoTracking().SingleAsync();
            story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"));
            Assert.Equal(0, await db.SaveChangesAsync());
        }

        await using var verification = database.CreateDbContext();
        var saved = await verification.ストーリー一覧.SingleAsync();
        Assert.Equal(new 公開範囲.全体公開(), saved.公開範囲);
    }

    [Theory]
    [InlineData("不明な公開範囲", "無料")]
    [InlineData("全体公開", "不明な料金")]
    public async Task DBの不正な列値は読み取り時に拒否する(string visibility, string price)
    {
        await using var database = await テスト用データベース.作成する();
        await using var db = database.CreateDbContext();
        await db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO "ストーリー一覧" ("ID", "作者", "公開範囲", "料金")
            VALUES ('story', 'author', {visibility}, {price})
            """
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => db.ストーリー一覧.SingleAsync());
    }
}
