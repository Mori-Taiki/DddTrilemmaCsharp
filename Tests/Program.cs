using Demo.Api;
using Demo.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
var checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var options = new DbContextOptionsBuilder<AppDb>().UseSqlite(connection).Options;
var factory = new Factory(options);
await using var db = new AppDb(options);
await db.Database.EnsureCreatedAsync();
foreach (var author in new[] { false, true })
foreach (var visibility in Enum.GetValues<Visibility>())
foreach (var price in Enum.GetValues<Price>())
foreach (var follow in new[] { false, true })
foreach (var vip in new[] { false, true })
foreach (var purchase in new[] { false, true })
{
    var story = Story.Create("story", "author", visibility, price);
    var viewer = author ? "author" : "reader";
    var expected = author || ((visibility == Visibility.Public || follow) && (price == Price.Free || vip || purchase));
    var expectedCalls = author ? 0 : (visibility == Visibility.Followers ? 1 : 0) + ((visibility == Visibility.Public || follow) && price == Price.Paid ? (vip ? 1 : 2) : 0);
    var fake = new Facts(follow, vip, purchase);
    var result = await new ViewingService(fake).Evaluate(story, viewer, default);
    Check(result.Allowed == expected && result.FactQueries == expectedCalls && fake.Calls == expectedCalls, "Decision table / short circuit");
    await db.Purchases.ExecuteDeleteAsync(); await db.Follows.ExecuteDeleteAsync(); await db.Members.ExecuteDeleteAsync(); await db.Stories.ExecuteDeleteAsync();
    db.ChangeTracker.Clear(); db.Stories.Add(story);
    if (follow) db.Follows.Add(new() { Viewer = viewer, Author = "author" });
    if (vip) db.Members.Add(new() { Viewer = viewer, Vip = true });
    if (purchase) db.Purchases.Add(new() { Viewer = viewer, StoryId = "story" });
    await db.SaveChangesAsync();
    var actual = await new ViewingService(new EfViewingFacts(factory)).Evaluate(story, viewer, default);
    Check(actual.Allowed == result.Allowed && actual.Reason == result.Reason && actual.Trace.SequenceEqual(result.Trace), "SQLite contract");
}
db.ChangeTracker.Clear();
var entity = await db.Stories.SingleAsync();
Check(entity.AuthorId == "author", "POCO materialization");
var original = entity.Visibility;
var target = original == Visibility.Public ? Visibility.Followers : Visibility.Public;
Check(entity.ChangeVisibility(target, "guest") is ChangeDenied && entity.Visibility == original, "Rejected mutation");
Check(entity.ChangeVisibility(target, "author") is Changed, "Accepted mutation");
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
entity = await db.Stories.SingleAsync();
Check(entity.Visibility == target, "Persisted change");
Check(entity.ChangeVisibility(target, "author") is ChangeDenied { Reason: "AlreadySet" }, "Duplicate change");
try
{
    await new ViewingService(new BrokenFacts()).Evaluate(Story.Create("s", "a", Visibility.Public, Price.Paid), "v", default);
    throw new Exception("Technical error swallowed");
}
catch (IOException) { checks++; }
Console.WriteLine($"PASS: {checks} assertions; 64 scenarios through fake + real SQLite; command persistence and technical failure.");
sealed class Factory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb> { public AppDb CreateDbContext() => new(options); }
sealed class Facts(bool follow, bool vip, bool purchase) : IViewingFacts
{
    public int Calls;
    public Task<bool> Follows(string v, string a, CancellationToken ct) { Calls++; return Task.FromResult(follow); }
    public Task<bool> IsVip(string v, CancellationToken ct) { Calls++; return Task.FromResult(vip); }
    public Task<bool> Purchased(string v, string s, CancellationToken ct) { Calls++; return Task.FromResult(purchase); }
}
sealed class BrokenFacts : IViewingFacts
{
    public Task<bool> Follows(string v, string a, CancellationToken ct) => throw new IOException("DB unavailable");
    public Task<bool> IsVip(string v, CancellationToken ct) => throw new IOException("DB unavailable");
    public Task<bool> Purchased(string v, string s, CancellationToken ct) => throw new IOException("DB unavailable");
}
