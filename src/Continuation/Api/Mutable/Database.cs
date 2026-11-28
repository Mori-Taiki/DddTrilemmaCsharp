using Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api;

public sealed class フォロー
{
    public string フォロワーID { get; set; } = "";
    public string 作者ID { get; set; } = "";
}

public sealed class 会員
{
    public string 利用者ID { get; set; } = "";
    public bool VIPである { get; set; }
}

public sealed class 購入
{
    public string 利用者ID { get; set; } = "";
    public ストーリーID ストーリーID { get; set; } = null!;
}

public sealed class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    // 永続化用の行を挟まず、ドメインエンティティを直接マッピングする。
    public DbSet<ストーリー> ストーリー一覧 => Set<ストーリー>();
    public DbSet<フォロー> フォロー関係 => Set<フォロー>();
    public DbSet<会員> 会員一覧 => Set<会員>();
    public DbSet<購入> 購入履歴 => Set<購入>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var story = b.Entity<ストーリー>();
        story.HasKey(x => x.ID);
        story
            .Property(x => x.ID)
            .HasConversion(x => x.値, x => new ストーリーID(x))
            .ValueGeneratedNever();
        story.Property(x => x.作者).HasConversion(x => x.値, x => new 利用者ID(x)).IsRequired();
        story
            .Property(x => x.公開範囲)
            .HasConversion(x => 境界変換.文字列にする(x), x => 公開範囲を復元する(x))
            .IsRequired();
        story
            .Property(x => x.料金区分)
            .HasConversion(x => 境界変換.文字列にする(x), x => 料金区分を復元する(x))
            .IsRequired();

        b.Entity<フォロー>().HasKey(x => new { x.フォロワーID, x.作者ID });
        b.Entity<会員>().HasKey(x => x.利用者ID);
        b.Entity<購入>().HasKey(x => new { x.利用者ID, x.ストーリーID });
        b.Entity<購入>()
            .Property(x => x.ストーリーID)
            .HasConversion(x => x.値, x => new ストーリーID(x));
        b.Entity<購入>().HasOne<ストーリー>().WithMany().HasForeignKey(x => x.ストーリーID);
    }

    private static 公開範囲 公開範囲を復元する(string value) =>
        境界変換.公開範囲に変換する(value)
        ?? throw new InvalidOperationException("DBの公開範囲が不正です。");

    private static 料金区分 料金区分を復元する(string value) =>
        境界変換.料金区分に変換する(value)
        ?? throw new InvalidOperationException("DBの料金区分が不正です。");
}

public sealed class EfCore閲覧事実取得(IDbContextFactory<AppDb> factory) : 閲覧事実取得
{
    public async Task<フォロー確認結果> フォローしているか(
        利用者ID viewer,
        利用者ID author,
        CancellationToken ct
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return new フォロー確認結果(
            await db
                .フォロー関係.AsNoTracking()
                .AnyAsync(x => x.フォロワーID == viewer.値 && x.作者ID == author.値, ct)
        );
    }

    public async Task<VIP会員確認結果> VIP会員か(利用者ID viewer, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return new VIP会員確認結果(
            await db
                .会員一覧.AsNoTracking()
                .AnyAsync(x => x.利用者ID == viewer.値 && x.VIPである, ct)
        );
    }

    public async Task<購入確認結果> 購入済みか(
        利用者ID viewer,
        ストーリーID storyId,
        CancellationToken ct
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return new 購入確認結果(
            await db
                .購入履歴.AsNoTracking()
                .AnyAsync(x => x.利用者ID == viewer.値 && x.ストーリーID == storyId, ct)
        );
    }
}

public static class 初期データ
{
    public static async Task 投入する(AppDb db)
    {
        await db.Database.EnsureCreatedAsync();
        if (await db.ストーリー一覧.AnyAsync())
            return;

        db.ストーリー一覧.AddRange(
            new ストーリー(
                new ストーリーID("public-free"),
                new 利用者ID("author"),
                new 公開範囲.全体公開(),
                new 料金区分.無料()
            ),
            new ストーリー(
                new ストーリーID("public-paid"),
                new 利用者ID("author"),
                new 公開範囲.全体公開(),
                new 料金区分.有料()
            ),
            new ストーリー(
                new ストーリーID("followers-free"),
                new 利用者ID("author"),
                new 公開範囲.フォロワー限定(),
                new 料金区分.無料()
            ),
            new ストーリー(
                new ストーリーID("followers-paid"),
                new 利用者ID("author"),
                new 公開範囲.フォロワー限定(),
                new 料金区分.有料()
            )
        );

        foreach (var follower in new[] { "follower", "vip", "buyer" })
            db.フォロー関係.Add(new() { フォロワーID = follower, 作者ID = "author" });

        db.会員一覧.Add(new() { 利用者ID = "vip", VIPである = true });

        foreach (var id in new[] { "public-paid", "followers-paid" })
            db.購入履歴.Add(new() { 利用者ID = "buyer", ストーリーID = new ストーリーID(id) });

        await db.SaveChangesAsync();
    }
}
