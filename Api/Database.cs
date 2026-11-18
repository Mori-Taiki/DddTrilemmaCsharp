using Demo.Domain;
using Microsoft.EntityFrameworkCore;
namespace Demo.Api;
public sealed class Follow { public string Viewer { get; set; } = ""; public string Author { get; set; } = ""; }
public sealed class Member { public string Viewer { get; set; } = ""; public bool Vip { get; set; } }
public sealed class Purchase { public string Viewer { get; set; } = ""; public string StoryId { get; set; } = ""; }
public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Story> Stories => Set<Story>();
    public DbSet<Follow> Follows => Set<Follow>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Story>().HasKey(x => x.Id);
        b.Entity<Story>().Property(x => x.AuthorId).IsRequired();
        b.Entity<Story>().Property(x => x.Visibility).HasConversion<string>();
        b.Entity<Story>().Property(x => x.Price).HasConversion<string>();
        b.Entity<Follow>().HasKey(x => new { x.Viewer, x.Author });
        b.Entity<Member>().HasKey(x => x.Viewer);
        b.Entity<Purchase>().HasKey(x => new { x.Viewer, x.StoryId });
        b.Entity<Purchase>().HasOne<Story>().WithMany().HasForeignKey(x => x.StoryId);
    }
}
public sealed class EfViewingFacts(IDbContextFactory<AppDb> factory) : IViewingFacts
{
    public async Task<bool> Follows(string viewer, string author, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Follows.AsNoTracking().AnyAsync(x => x.Viewer == viewer && x.Author == author, ct);
    }
    public async Task<bool> IsVip(string viewer, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Members.AsNoTracking().AnyAsync(x => x.Viewer == viewer && x.Vip, ct);
    }
    public async Task<bool> Purchased(string viewer, string story, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Purchases.AsNoTracking().AnyAsync(x => x.Viewer == viewer && x.StoryId == story, ct);
    }
}
public static class Seed
{
    public static async Task Initialize(AppDb db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.Stories.AnyAsync()) return;
        db.Stories.AddRange(Story.Create("public-free", "author", Visibility.Public, Price.Free),
            Story.Create("public-paid", "author", Visibility.Public, Price.Paid),
            Story.Create("followers-free", "author", Visibility.Followers, Price.Free),
            Story.Create("followers-paid", "author", Visibility.Followers, Price.Paid));
        foreach (var viewer in new[] { "follower", "vip", "buyer" }) db.Follows.Add(new() { Viewer = viewer, Author = "author" });
        db.Members.Add(new() { Viewer = "vip", Vip = true });
        foreach (var id in new[] { "public-paid", "followers-paid" }) db.Purchases.Add(new() { Viewer = "buyer", StoryId = id });
        await db.SaveChangesAsync();
    }
}
