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
    [InlineData("/stories/public-free/view", HttpStatusCode.BadRequest)]
    [InlineData("/stories/missing/view?viewer=guest", HttpStatusCode.NotFound)]
    public async Task 閲覧者の未指定は400で作品の不存在は404になる(
        string path,
        HttpStatusCode expectedStatus
    )
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task 全体公開の無料作品はフォローも購入もない閲覧者に200を返す()
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
    }

    [Fact]
    public async Task 限定作品は非フォロワーに403を返す()
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
    }

    [Fact]
    public async Task 限定有料作品はフォロー済みVIPに200を返す()
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
        await database.保存する(new フォロー { 閲覧者 = "reader", 作者 = "author" });
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = true });
        // 購入は保存しない。
        await using var app = new テスト用アプリ(database);
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/stories/story/view?viewer=reader");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task 限定有料作品はフォロー済みの一般会員の購入者に200を返す()
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
        await database.保存する(new フォロー { 閲覧者 = "reader", 作者 = "author" });
        await database.保存する(new 会員 { 閲覧者 = "reader", VIPである = false });
        await database.購入を保存する("reader", "story");
        await using var app = new テスト用アプリ(database);
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/stories/story/view?viewer=reader");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
