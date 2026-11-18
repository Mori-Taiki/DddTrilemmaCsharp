using System.Text.Json.Serialization;
using Demo.Api;
using Demo.Domain;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
var dbPath = Path.GetFullPath(builder.Configuration["DatabasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "app.db"));
builder.Services.AddDbContextFactory<AppDb>(o => o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()));
builder.Services.AddScoped<閲覧事実取得, EfCore閲覧事実取得>();
builder.Services.AddScoped<閲覧判定サービス>();
var app = builder.Build();
app.UseExceptionHandler();
await using (var scope = app.Services.CreateAsyncScope())
{
    await 初期データ.投入する(scope.ServiceProvider.GetRequiredService<AppDb>());
}
app.MapGet("/", () => new { app = "DDD Decision demo", endpoints = new[] { "GET /stories", "GET /stories/{id}/view?viewer=guest", "PUT /stories/{id}/visibility" } });
app.MapGet("/stories", async (AppDb db, CancellationToken ct) => await db.ストーリー一覧.AsNoTracking().OrderBy(x => x.ID).Select(x => new { id = x.ID, authorId = x.作者, visibility = x.公開範囲, price = x.料金 }).ToListAsync(ct));
app.MapGet("/stories/{id}/view", async Task<IResult> (string id, string? viewer, AppDb db, 閲覧判定サービス service, ILogger<閲覧判定サービス> log, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(viewer)) return Results.BadRequest(new { error = "viewer is required" });
    var story = await db.ストーリー一覧.AsNoTracking().SingleOrDefaultAsync(x => x.ID == id, ct);
    if (story is null) return Results.NotFound();
    var result = await service.判定する(story, viewer, ct);
    log.LogInformation("Decision {Trace}; fact queries={Count} (story load excluded)", string.Join(" -> ", result.判定経路), result.事実取得回数);
    return Results.Json(result, statusCode: result.許可 ? 200 : 403);
});
app.MapPut("/stories/{id}/visibility", async Task<IResult> (string id, 公開範囲変更要求 request, AppDb db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.実行者) || request.公開範囲 is null || !Enum.IsDefined(request.公開範囲.Value)) return Results.BadRequest(new { error = "actor and valid visibility are required" });
    var story = await db.ストーリー一覧.SingleOrDefaultAsync(x => x.ID == id, ct);
    if (story is null) return Results.NotFound();
    var result = story.公開範囲を変える(request.公開範囲.Value, request.実行者);
    if (result is 変更できた) await db.SaveChangesAsync(ct);
    return result switch
    {
        変更できた => Results.Ok(new { id = story.ID, visibility = story.公開範囲 }),
        変更拒否 d => Results.Json(new { reason = d.理由 }, statusCode: d.理由 == 変更拒否理由.作者ではない ? 403 : 409)
    };
});
app.Run();
public sealed record 公開範囲変更要求([property: JsonPropertyName("actor")] string? 実行者, [property: JsonPropertyName("visibility")] 公開範囲? 公開範囲);
