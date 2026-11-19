using System.Net;
using System.Net.Http.Json;
using Demo.Api;
using Demo.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Tests;

public sealed class Apiテスト
{
    [Fact]
    public async Task 作者の公開範囲変更が保存され次の閲覧判定に反映される()
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/stories/public-free/visibility",
            new { actor = "author", visibility = "フォロワー限定" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var saved = await db.ストーリー一覧.AsNoTracking().SingleAsync(x => x.ID == "public-free");
        Assert.Equal("author", saved.作者);
        Assert.Equal("無料", saved.料金);
        Assert.Equal("フォロワー限定", saved.公開範囲);
        using var viewing = await client.GetAsync("/stories/public-free/view?viewer=guest");
        Assert.Equal(HttpStatusCode.Forbidden, viewing.StatusCode);
        var body = await viewing.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("フォローが必要", body.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task 作者以外の変更は拒否され保存済みの公開範囲は変わらない()
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/stories/public-free/visibility",
            new { actor = "guest", visibility = "フォロワー限定" }
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var saved = await db.ストーリー一覧.AsNoTracking().SingleAsync(x => x.ID == "public-free");
        Assert.Equal("全体公開", saved.公開範囲);
    }

    [Fact]
    public async Task 同じ公開範囲への変更は競合として返される()
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/stories/public-free/visibility",
            new { actor = "author", visibility = "全体公開" }
        );

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("既にその公開範囲", body.GetProperty("reason").GetString());
    }

    [Theory]
    [InlineData("閲覧者が未指定", "/stories/public-free/view", HttpStatusCode.BadRequest)]
    [InlineData("存在しない作品", "/stories/missing/view?viewer=guest", HttpStatusCode.NotFound)]
    [InlineData("無料公開の閲覧", "/stories/public-free/view?viewer=guest", HttpStatusCode.OK)]
    [InlineData(
        "非フォロワーの限定作品閲覧",
        "/stories/followers-paid/view?viewer=guest",
        HttpStatusCode.Forbidden
    )]
    [InlineData(
        "フォロー済みVIPの閲覧",
        "/stories/followers-paid/view?viewer=vip",
        HttpStatusCode.OK
    )]
    [InlineData(
        "フォロー済み購入者の閲覧",
        "/stories/followers-paid/view?viewer=buyer",
        HttpStatusCode.OK
    )]
    public async Task 閲覧要求に適切なHTTPステータスを返す(
        string caseName,
        string path,
        HttpStatusCode expectedStatus
    )
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.True(response.StatusCode == expectedStatus, caseName);
    }

    [Theory]
    [InlineData("未知の公開範囲", "{\"actor\":\"author\",\"visibility\":\"作者限定\"}")]
    [InlineData("空の実行者", "{\"actor\":\" \",\"visibility\":\"全体公開\"}")]
    [InlineData("公開範囲が未指定", "{\"actor\":\"author\"}")]
    [InlineData("未定義の公開範囲", "{\"actor\":\"author\",\"visibility\":99}")]
    [InlineData("実行者が未指定", "{\"visibility\":\"全体公開\"}")]
    public async Task 不正な変更要求は入力エラーになる(string caseName, string json)
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PutAsync("/stories/public-free/visibility", content);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, caseName);
    }
}
