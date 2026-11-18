using Demo.Domain;
using Microsoft.EntityFrameworkCore;
namespace Demo.Api;
public sealed class フォロー { public string 閲覧者 { get; set; } = ""; public string 作者 { get; set; } = ""; }
public sealed class 会員 { public string 閲覧者 { get; set; } = ""; public bool VIPである { get; set; } }
public sealed class 購入 { public string 閲覧者 { get; set; } = ""; public string ストーリーID { get; set; } = ""; }
public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<ストーリー> ストーリー一覧 => Set<ストーリー>();
    public DbSet<フォロー> フォロー関係 => Set<フォロー>();
    public DbSet<会員> 会員一覧 => Set<会員>();
    public DbSet<購入> 購入一覧 => Set<購入>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<ストーリー>().HasKey(x => x.ID);
        b.Entity<ストーリー>().Property(x => x.作者).IsRequired();
        b.Entity<ストーリー>().Property(x => x.公開範囲).HasConversion<string>();
        b.Entity<ストーリー>().Property(x => x.料金).HasConversion<string>();
        b.Entity<フォロー>().HasKey(x => new { x.閲覧者, x.作者 });
        b.Entity<会員>().HasKey(x => x.閲覧者);
        b.Entity<購入>().HasKey(x => new { x.閲覧者, x.ストーリーID });
        b.Entity<購入>().HasOne<ストーリー>().WithMany().HasForeignKey(x => x.ストーリーID);
    }
}
public sealed class EfCore閲覧事実取得(IDbContextFactory<AppDb> factory) : 閲覧事実取得
{
    public async Task<bool> フォローしているか(string viewer, string author, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.フォロー関係.AsNoTracking().AnyAsync(x => x.閲覧者 == viewer && x.作者 == author, ct);
    }
    public async Task<bool> VIPか(string viewer, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.会員一覧.AsNoTracking().AnyAsync(x => x.閲覧者 == viewer && x.VIPである, ct);
    }
    public async Task<bool> 購入済みか(string viewer, string storyId, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.購入一覧.AsNoTracking().AnyAsync(x => x.閲覧者 == viewer && x.ストーリーID == storyId, ct);
    }
}
public static class 初期データ
{
    public static async Task 投入する(AppDb db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.ストーリー一覧.AnyAsync()) return;
        db.ストーリー一覧.AddRange(ストーリー.投稿する("public-free", "author", 公開範囲.全体公開, 料金区分.無料),
            ストーリー.投稿する("public-paid", "author", 公開範囲.全体公開, 料金区分.有料),
            ストーリー.投稿する("followers-free", "author", 公開範囲.フォロワー限定, 料金区分.無料),
            ストーリー.投稿する("followers-paid", "author", 公開範囲.フォロワー限定, 料金区分.有料));
        foreach (var viewer in new[] { "follower", "vip", "buyer" }) db.フォロー関係.Add(new() { 閲覧者 = viewer, 作者 = "author" });
        db.会員一覧.Add(new() { 閲覧者 = "vip", VIPである = true });
        foreach (var id in new[] { "public-paid", "followers-paid" }) db.購入一覧.Add(new() { 閲覧者 = "buyer", ストーリーID = id });
        await db.SaveChangesAsync();
    }
}
