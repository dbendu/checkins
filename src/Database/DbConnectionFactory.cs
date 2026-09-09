using Database.Config;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace Database;

public interface IDbConnectionFactory
{
    Task<SqliteConnection> OpenAsync(CancellationToken token);
}

public sealed class DbConnectionFactory(IOptions<DatabaseConfig> config) : IDbConnectionFactory
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = config.Value.Path,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = true,
    }.ToString();

    public async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(token);

        // Внешние ключи SQLite по умолчанию не проверяет: без этой строки
        // references в схеме останется декорацией. Настройка живёт в соединении,
        // поэтому ставится каждый раз, а не однажды при миграции.
        await using var pragma = connection.CreateCommand();
        pragma.CommandText = "pragma foreign_keys = on; pragma busy_timeout = 5000;";
        await pragma.ExecuteNonQueryAsync(token);

        return connection;
    }
}
