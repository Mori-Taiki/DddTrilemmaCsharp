using System.Net;
using System.Net.Http.Json;
using Demo.Api;
using Demo.Domain;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Tests;

public sealed class 閲覧のHTTPテスト
{
    [Fact]
    public async Task 事実取得の障害は業務上の拒否ではなくサーバーエラーとして返す()
    {
        await using var app = new テスト用アプリ();
        await using var failingApp = app.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddScoped<閲覧事実取得, 障害を起こす事実取得>()
            )
        );
        using var client = failingApp.CreateClient();

        using var response = await client.GetAsync("/stories/public-paid/view?viewer=guest");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(500, body.GetProperty("status").GetInt32());
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
