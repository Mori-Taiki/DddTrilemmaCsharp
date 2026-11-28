using System.Text.Json.Serialization;
using Demo.Api;
using Demo.Domain;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
);
builder.Services.AddProblemDetails();

var dbPath = Path.GetFullPath(
    builder.Configuration["DatabasePath"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "app.db")
);
builder.Services.AddDbContextFactory<AppDb>(o =>
    o.UseSqlite(
        new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = dbPath }.ToString()
    )
);
builder.Services.AddScoped<閲覧事実取得, EfCore閲覧事実取得>();
builder.Services.AddScoped<閲覧判定サービス>();

var app = builder.Build();
app.UseExceptionHandler();

await using (var scope = app.Services.CreateAsyncScope())
{
    await 初期データ.投入する(scope.ServiceProvider.GetRequiredService<AppDb>());
}

app.MapGet("/", () =>
    new
    {
        app = "DDD Decision demo",
        endpoints = new[]
        {
            "GET /stories",
            "GET /stories/{id}/view?viewer=guest",
            "PUT /stories/{id}/visibility",
        },
    }
);

app.ストーリーAPIを登録する();

app.Run();

public partial class Program { }
