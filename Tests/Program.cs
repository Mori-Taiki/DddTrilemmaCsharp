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
foreach (var 作者本人 in new[] { false, true })
foreach (var 公開範囲 in Enum.GetValues<公開範囲>())
foreach (var 料金 in Enum.GetValues<料金区分>())
foreach (var フォローしている in new[] { false, true })
foreach (var VIPである in new[] { false, true })
foreach (var 購入済み in new[] { false, true })
{
    var 対象 = ストーリー.投稿する("story", "author", 公開範囲, 料金);
    var 閲覧者 = 作者本人 ? "author" : "reader";
    var 期待する許可 = 作者本人 || ((公開範囲 == 公開範囲.全体公開 || フォローしている) && (料金 == 料金区分.無料 || VIPである || 購入済み));
    var 期待する取得回数 = 作者本人 ? 0 : (公開範囲 == 公開範囲.フォロワー限定 ? 1 : 0) + ((公開範囲 == 公開範囲.全体公開 || フォローしている) && 料金 == 料金区分.有料 ? (VIPである ? 1 : 2) : 0);
    var 偽物の事実取得 = new 偽物の閲覧事実取得(フォローしている, VIPである, 購入済み);
    var 結果 = await new 閲覧判定サービス(偽物の事実取得).判定する(対象, 閲覧者, default);
    Check(結果.許可 == 期待する許可 && 結果.事実取得回数 == 期待する取得回数 && 偽物の事実取得.呼出回数 == 期待する取得回数, "Decision table / short circuit");
    await db.購入一覧.ExecuteDeleteAsync(); await db.フォロー関係.ExecuteDeleteAsync(); await db.会員一覧.ExecuteDeleteAsync(); await db.ストーリー一覧.ExecuteDeleteAsync();
    db.ChangeTracker.Clear(); db.ストーリー一覧.Add(対象);
    if (フォローしている) db.フォロー関係.Add(new() { 閲覧者 = 閲覧者, 作者 = "author" });
    if (VIPである) db.会員一覧.Add(new() { 閲覧者 = 閲覧者, VIPである = true });
    if (購入済み) db.購入一覧.Add(new() { 閲覧者 = 閲覧者, ストーリーID = "story" });
    await db.SaveChangesAsync();
    var 実際 = await new 閲覧判定サービス(new EfCore閲覧事実取得(factory)).判定する(対象, 閲覧者, default);
    Check(実際.許可 == 結果.許可 && 実際.理由 == 結果.理由 && 実際.判定経路.SequenceEqual(結果.判定経路), "SQLite contract");
}
db.ChangeTracker.Clear();
var 変更対象 = await db.ストーリー一覧.SingleAsync();
Check(変更対象.作者 == "author", "POCO materialization");
var 変更前 = 変更対象.公開範囲;
var 変更先 = 変更前 == 公開範囲.全体公開 ? 公開範囲.フォロワー限定 : 公開範囲.全体公開;
Check(変更対象.公開範囲を変える(変更先, "guest") is 変更拒否 && 変更対象.公開範囲 == 変更前, "Rejected mutation");
Check(変更対象.公開範囲を変える(変更先, "author") is 変更できた, "Accepted mutation");
await db.SaveChangesAsync(); db.ChangeTracker.Clear();
変更対象 = await db.ストーリー一覧.SingleAsync();
Check(変更対象.公開範囲 == 変更先, "Persisted change");
Check(変更対象.公開範囲を変える(変更先, "author") is 変更拒否 { 理由: 変更拒否理由.既にその公開範囲 }, "Duplicate change");
try
{
    await new 閲覧判定サービス(new 失敗する閲覧事実取得()).判定する(ストーリー.投稿する("s", "a", 公開範囲.全体公開, 料金区分.有料), "v", default);
    throw new Exception("Technical error swallowed");
}
catch (IOException) { checks++; }
Console.WriteLine($"PASS: {checks} assertions; 64 scenarios through fake + real SQLite; command persistence and technical failure.");
sealed class Factory(DbContextOptions<AppDb> options) : IDbContextFactory<AppDb> { public AppDb CreateDbContext() => new(options); }
sealed class 偽物の閲覧事実取得(bool フォローしている, bool VIPである, bool 購入済み) : 閲覧事実取得
{
    public int 呼出回数;
    public Task<bool> フォローしているか(string v, string a, CancellationToken ct) { 呼出回数++; return Task.FromResult(フォローしている); }
    public Task<bool> VIPか(string v, CancellationToken ct) { 呼出回数++; return Task.FromResult(VIPである); }
    public Task<bool> 購入済みか(string v, string s, CancellationToken ct) { 呼出回数++; return Task.FromResult(購入済み); }
}
sealed class 失敗する閲覧事実取得 : 閲覧事実取得
{
    public Task<bool> フォローしているか(string v, string a, CancellationToken ct) => throw new IOException("DB unavailable");
    public Task<bool> VIPか(string v, CancellationToken ct) => throw new IOException("DB unavailable");
    public Task<bool> 購入済みか(string v, string s, CancellationToken ct) => throw new IOException("DB unavailable");
}
