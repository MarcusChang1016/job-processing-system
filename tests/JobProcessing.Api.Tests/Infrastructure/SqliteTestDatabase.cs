using JobProcessing.Api.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobProcessing.Api.Tests.Infrastructure;

internal sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection, AppDbContext dbContext)
    {
        _connection = connection;
        DbContext = dbContext;
    }

    public AppDbContext DbContext { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        AppDbContext? dbContext = null;

        var connection = new SqliteConnection("Data Source=:memory:");

        try
        {
            await connection.OpenAsync();

            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            dbContext = new AppDbContext(dbOptions);
            await dbContext.Database.EnsureCreatedAsync();

            return new SqliteTestDatabase(connection, dbContext);
        }
        catch
        {
            try
            {
                if (dbContext is not null)
                    await dbContext.DisposeAsync();
            }
            finally
            {
                await connection.DisposeAsync();
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await DbContext.DisposeAsync();
        }
        finally
        {
            await _connection.DisposeAsync();
        }
    }
}
