using System.Net.Http.Json;

namespace Demo.Tests;

public sealed class 案内と一覧のHTTPテスト
{
    [Fact]
    public async Task ルートは利用可能なAPIを案内する()
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        var body = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/");

        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("app").GetString()));
        Assert.Equal(
            new[]
            {
                "GET /stories",
                "GET /stories/{id}/view?viewer=guest",
                "PUT /stories/{id}/visibility",
            },
            body.GetProperty("endpoints").EnumerateArray().Select(x => x.GetString())
        );
    }

    [Fact]
    public async Task 一覧はストーリーIDの昇順で全ストーリーを返す()
    {
        await using var app = new テスト用アプリ();
        using var client = app.CreateClient();

        var stories = await client.GetFromJsonAsync<System.Text.Json.JsonElement[]>("/stories");

        Assert.NotNull(stories);
        Assert.Equal(
            new[] { "followers-free", "followers-paid", "public-free", "public-paid" },
            stories.Select(story => story.GetProperty("id").GetString())
        );
    }
}
