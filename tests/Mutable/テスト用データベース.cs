using System.Data.Common;
using Demo.Api;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Demo.Tests;

// テストごとに独立した接続を持ち、複数のDbContextから同じ実SQLiteを利用する。
internal sealed class テスト用データベース : IAsyncDisposable, IDbContextFactory<AppDb>
{
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<AppDb> options;
    private readonly クエリ計測 counter = new();

    private テスト用データベース(SqliteConnection connection)
    {
        this.connection = connection;
        options = new DbContextOptionsBuilder<AppDb>()
            .UseSqlite(connection)
            .AddInterceptors(counter)
            .Options;
    }

    public static async Task<テスト用データベース> 作成する()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var database = new テスト用データベース(connection);
        await using var db = database.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        return database;
    }

    public AppDb CreateDbContext() => new(options);

    public int クエリ数 => counter.count;

    public void 計測を開始する() => counter.count = 0;

    public async Task 保存する(params object[] entities)
    {
        await using var db = CreateDbContext();
        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private sealed class クエリ計測 : DbCommandInterceptor
    {
        public int count;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            count++;
            return ValueTask.FromResult(result);
        }
    }
}
