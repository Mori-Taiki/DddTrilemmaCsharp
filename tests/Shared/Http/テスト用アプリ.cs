using Demo.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Demo.Tests;

internal sealed class テスト用アプリ : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "ddd-tests-" + Guid.NewGuid()
    );

    private readonly IDbContextFactory<AppDb>? database;

    public テスト用アプリ(IDbContextFactory<AppDb>? database = null)
    {
        this.database = database;
        Directory.CreateDirectory(directory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (database is not null)
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IDbContextFactory<AppDb>>(database)
            );
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("DatabasePath", Path.Combine(directory, "test.db"));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(directory, recursive: true);
    }
}
