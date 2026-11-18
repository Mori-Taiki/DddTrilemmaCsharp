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
app.MapGet("/stories/{id}/view", async Task<IResult> (string id, [Microsoft.AspNetCore.Mvc.FromQuery(Name = "viewer")] string? 閲覧者, AppDb db, 閲覧判定サービス 判定サービス, ILogger<閲覧判定サービス> log, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(閲覧者)) return Results.BadRequest(new { error = "viewer is required" });
    var 対象 = await db.ストーリー一覧.AsNoTracking().SingleOrDefaultAsync(x => x.ID == id, ct);
    if (対象 is null) return Results.NotFound();
    var 結果 = await 判定サービス.判定する(対象, 閲覧者, ct);
    log.LogInformation("Decision {Trace}; fact queries={Count} (story load excluded)", string.Join(" -> ", 結果.判定経路), 結果.事実取得回数);
    return Results.Json(結果, statusCode: 結果.許可 ? 200 : 403);
});
app.MapPut("/stories/{id}/visibility", async Task<IResult> (string id, 公開範囲変更要求 要求, AppDb db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(要求.実行者) || 要求.公開範囲 is null || !Enum.IsDefined(要求.公開範囲.Value)) return Results.BadRequest(new { error = "actor and valid visibility are required" });
    var 対象 = await db.ストーリー一覧.SingleOrDefaultAsync(x => x.ID == id, ct);
    if (対象 is null) return Results.NotFound();
    var 結果 = 対象.公開範囲を変える(要求.公開範囲.Value, 要求.実行者);
    if (結果 is 変更できた) await db.SaveChangesAsync(ct);
    return 結果 switch
    {
        変更できた => Results.Ok(new { id = 対象.ID, visibility = 対象.公開範囲 }),
        変更拒否 d => Results.Json(new { reason = d.理由 }, statusCode: d.理由 == 変更拒否理由.作者ではない ? 403 : 409)
    };
});
app.Run();
public sealed record 公開範囲変更要求([property: JsonPropertyName("actor")] string? 実行者, [property: JsonPropertyName("visibility")] 公開範囲? 公開範囲);
