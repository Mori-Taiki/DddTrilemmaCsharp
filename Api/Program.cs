using System.Text.Json.Serialization;
using Demo.Api;
using Demo.Domain;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
var dbPath = Path.GetFullPath(builder.Configuration["DatabasePath"] ?? Path.Combine(builder.Environment.ContentRootPath, "app.db"));
builder.Services.AddDbContextFactory<AppDb>(o => o.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()));
builder.Services.AddScoped<IViewingFacts, EfViewingFacts>();
builder.Services.AddScoped<ViewingService>();
var app = builder.Build();
app.UseExceptionHandler();
await using (var scope = app.Services.CreateAsyncScope())
{
    await Seed.Initialize(scope.ServiceProvider.GetRequiredService<AppDb>());
}
app.MapGet("/", () => new { app = "DDD Decision demo", endpoints = new[] { "GET /stories", "GET /stories/{id}/view?viewer=guest", "PUT /stories/{id}/visibility" } });
app.MapGet("/stories", async (AppDb db, CancellationToken ct) => await db.Stories.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct));
app.MapGet("/stories/{id}/view", async Task<IResult> (string id, string? viewer, AppDb db, ViewingService service, ILogger<ViewingService> log, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(viewer)) return Results.BadRequest(new { error = "viewer is required" });
    var story = await db.Stories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    if (story is null) return Results.NotFound();
    var result = await service.Evaluate(story, viewer, ct);
    log.LogInformation("Decision {Trace}; fact queries={Count} (story load excluded)", string.Join(" -> ", result.Trace), result.FactQueries);
    return Results.Json(result, statusCode: result.Allowed ? 200 : 403);
});
app.MapPut("/stories/{id}/visibility", async Task<IResult> (string id, ChangeRequest request, AppDb db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Actor) || request.Visibility is null || !Enum.IsDefined(request.Visibility.Value)) return Results.BadRequest(new { error = "actor and valid visibility are required" });
    var story = await db.Stories.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (story is null) return Results.NotFound();
    var result = story.ChangeVisibility(request.Visibility.Value, request.Actor);
    if (result is Changed) await db.SaveChangesAsync(ct);
    return result switch
    {
        Changed => Results.Ok(new { story.Id, story.Visibility }),
        ChangeDenied d => Results.Json(new { reason = d.Reason }, statusCode: d.Reason == "NotAuthor" ? 403 : 409)
    };
});
app.Run();
public sealed record ChangeRequest(string? Actor, Visibility? Visibility);
