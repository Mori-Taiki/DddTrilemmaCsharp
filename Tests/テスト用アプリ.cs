using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace Demo.Tests;

internal sealed class テスト用アプリ : WebApplicationFactory<Program>
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(),
        "ddd-tests-" + Guid.NewGuid()
    );

    public テスト用アプリ() => Directory.CreateDirectory(directory);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
