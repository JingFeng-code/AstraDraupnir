using Draupnir.Api;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Draupnir.Api.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly string name = $"draupnir_test_{Guid.NewGuid():N}";
    private NpgsqlConnectionStringBuilder? server;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    /// <summary>測試資料庫連線字串：讓測試可以直接查 invariant（例如有效 Admin 數量）。</summary>
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        server = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("DRAUPNIR_TEST_POSTGRES")
            ?? "Host=127.0.0.1;Port=55432;Database=postgres;Username=postgres") { Pooling = false };
        await Execute($"CREATE DATABASE \"{name}\"");
        var database = new NpgsqlConnectionStringBuilder(server.ConnectionString) { Database = name };
        var options = new DbContextOptionsBuilder<DraupnirDb>().UseNpgsql(database.ConnectionString).Options;
        await using (var db = new DraupnirDb(options)) await db.Database.MigrateAsync();
        ConnectionString = database.ConnectionString;
        Factory = new TestHost(database.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null) await Factory.DisposeAsync();
        if (server is not null) await Execute($"DROP DATABASE IF EXISTS \"{name}\"");
    }

    private async Task Execute(string sql)
    {
        await using var connection = new NpgsqlConnection(server!.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class TestHost(string connection) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Draupnir", connection);
            builder.UseSetting("DRAUPNIR_ADMIN_EMAIL", "admin@example.test");
            builder.UseSetting("DRAUPNIR_ADMIN_PASSWORD", "local-fixture-password");
            builder.UseSetting("DRAUPNIR_STYLUS_EXECUTABLE", Environment.GetEnvironmentVariable("DRAUPNIR_TEST_STYLUS_EXECUTABLE") ?? "");
            builder.UseSetting("DRAUPNIR_REPORT_DIR", Path.Combine(Path.GetTempPath(), "draupnir_reports_" + Guid.NewGuid().ToString("N")));
        }
    }
}
