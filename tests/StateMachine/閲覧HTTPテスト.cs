using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Demo.Api;
using Demo.Domain;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Demo.Tests;

public sealed class 閲覧のHTTPテスト
{
    [Fact]
    public async Task 閲覧許可は200と判定結果をJSONで返す()
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
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        // 購入は保存しない。
        await using var app = new テスト用アプリ(database);
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/stories/story/view?viewer=reader");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("allowed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("reason").ValueKind);
        Assert.Equal(
            new[] { "許可" },
            body.GetProperty("trace").EnumerateArray().Select(x => x.GetString())
        );
        Assert.Equal(0, body.GetProperty("factQueries").GetInt32());
    }

    [Fact]
    public async Task 閲覧拒否は403と拒否理由をJSONで返す()
    {
        await using var database = await テスト用データベース.作成する();
        await database.保存する(
            new ストーリー(
                new ストーリーID("story"),
                new 利用者ID("author"),
                new 公開範囲.フォロワー限定(),
                new 料金区分.有料()
            )
        );
        // フォローは保存しない。
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        // 購入は保存しない。
        await using var app = new テスト用アプリ(database);
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/stories/story/view?viewer=reader");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("allowed").GetBoolean());
        Assert.Equal("フォローが必要", body.GetProperty("reason").GetString());
        Assert.Equal(
            new[] { "フォロー確認が必要", "拒否" },
            body.GetProperty("trace").EnumerateArray().Select(x => x.GetString())
        );
        Assert.Equal(1, body.GetProperty("factQueries").GetInt32());
    }

    [Fact]
    public async Task 閲覧者が未指定なら400と入力エラーを返す()
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/stories/public-free/view");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("viewer is required", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task 存在しない作品には404を返す()
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/stories/missing/view?viewer=reader");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

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
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(500, body.GetProperty("status").GetInt32());
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
