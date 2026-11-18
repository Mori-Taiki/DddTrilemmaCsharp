using Demo.Api;
using Demo.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var checks = 0;
void 検証する(bool condition, string message)
{
    checks++;
    if (!condition)
        throw new Exception(message);
}

await using var connection = new SqliteConnection("Data Source=:memory:");

await connection.OpenAsync();
var options = new DbContextOptionsBuilder<AppDb>().UseSqlite(connection).Options;
var factory = new テスト用DbContextFactory(options);
await using var db = new AppDb(options);

await db.Database.EnsureCreatedAsync();

foreach (var isAuthor in new[] { false, true })
foreach (var visibility in Enum.GetValues<公開範囲>())
foreach (var price in Enum.GetValues<料金区分>())
foreach (var isFollowing in new[] { false, true })
foreach (var isVip in new[] { false, true })
foreach (var hasPurchased in new[] { false, true })
{
    var story = ストーリー.投稿する("story", "author", visibility, price);
    var viewer = isAuthor ? "author" : "reader";
    var expectedAllowed =
        isAuthor
        || (
            (visibility == 公開範囲.全体公開 || isFollowing)
            && (price == 料金区分.無料 || isVip || hasPurchased)
        );
    var expectedQueryCount = isAuthor
        ? 0
        : (visibility == 公開範囲.フォロワー限定 ? 1 : 0)
            + (
                (visibility == 公開範囲.全体公開 || isFollowing) && price == 料金区分.有料
                    ? (isVip ? 1 : 2)
                    : 0
            );

    var fakeFacts = new 偽物の閲覧事実取得(isFollowing, isVip, hasPurchased);
    var result = await new 閲覧判定サービス(fakeFacts).判定する(story, viewer, default);
    検証する(
        result.許可 == expectedAllowed
            && result.事実取得回数 == expectedQueryCount
            && fakeFacts.callCount == expectedQueryCount,
        "Decision table / short circuit"
    );

    await db.購入一覧.ExecuteDeleteAsync();
    await db.フォロー関係.ExecuteDeleteAsync();
    await db.会員一覧.ExecuteDeleteAsync();
    await db.ストーリー一覧.ExecuteDeleteAsync();

    db.ChangeTracker.Clear();
    db.ストーリー一覧.Add(story);
    if (isFollowing)
        db.フォロー関係.Add(new() { 閲覧者 = viewer, 作者 = "author" });
    if (isVip)
        db.会員一覧.Add(new() { 閲覧者 = viewer, VIPである = true });
    if (hasPurchased)
        db.購入一覧.Add(new() { 閲覧者 = viewer, ストーリーID = "story" });

    await db.SaveChangesAsync();

    var actual = await new 閲覧判定サービス(new EfCore閲覧事実取得(factory)).判定する(
        story,
        viewer,
        default
    );
    検証する(
        actual.許可 == result.許可
            && actual.理由 == result.理由
            && actual.判定経路.SequenceEqual(result.判定経路),
        "SQLite contract"
    );
}

db.ChangeTracker.Clear();
var updatedStory = await db.ストーリー一覧.SingleAsync();
検証する(updatedStory.作者 == "author", "POCO materialization");

var originalVisibility = updatedStory.公開範囲;
var newVisibility =
    originalVisibility == 公開範囲.全体公開 ? 公開範囲.フォロワー限定 : 公開範囲.全体公開;
検証する(
    updatedStory.公開範囲を変える(newVisibility, "guest") is 変更拒否
        && updatedStory.公開範囲 == originalVisibility,
    "Rejected mutation"
);
検証する(updatedStory.公開範囲を変える(newVisibility, "author") is 変更できた, "Accepted mutation");

await db.SaveChangesAsync();

db.ChangeTracker.Clear();

updatedStory = await db.ストーリー一覧.SingleAsync();
検証する(updatedStory.公開範囲 == newVisibility, "Persisted change");
検証する(
    updatedStory.公開範囲を変える(newVisibility, "author")
        is 変更拒否 { 理由: 変更拒否理由.既にその公開範囲 },
    "Duplicate change"
);

try
{
    await new 閲覧判定サービス(new 失敗する閲覧事実取得()).判定する(
        ストーリー.投稿する("s", "a", 公開範囲.全体公開, 料金区分.有料),
        "v",
        default
    );
    throw new Exception("Technical error swallowed");
}
catch (IOException)
{
    checks++;
}

Console.WriteLine(
    $"PASS: {checks} assertions; 64 scenarios through fake + real SQLite; command persistence and technical failure."
);

sealed class テスト用DbContextFactory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb>
{
    public AppDb CreateDbContext() => new(options);
}

sealed class 偽物の閲覧事実取得(bool isFollowing, bool isVip, bool hasPurchased) : 閲覧事実取得
{
    public int callCount;

    public Task<bool> フォローしているか(string viewer, string author, CancellationToken ct)
    {
        callCount++;
        return Task.FromResult(isFollowing);
    }

    public Task<bool> VIPか(string viewer, CancellationToken ct)
    {
        callCount++;
        return Task.FromResult(isVip);
    }

    public Task<bool> 購入済みか(string viewer, string storyId, CancellationToken ct)
    {
        callCount++;
        return Task.FromResult(hasPurchased);
    }
}

sealed class 失敗する閲覧事実取得 : 閲覧事実取得
{
    public Task<bool> フォローしているか(string viewer, string author, CancellationToken ct) =>
        throw new IOException("DB unavailable");

    public Task<bool> VIPか(string viewer, CancellationToken ct) =>
        throw new IOException("DB unavailable");

    public Task<bool> 購入済みか(string viewer, string storyId, CancellationToken ct) =>
        throw new IOException("DB unavailable");
}
