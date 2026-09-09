using System.Reflection;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Database;

public sealed class Migrator(IDbConnectionFactory factory, ILogger<Migrator> logger)
{
    private const string ResourcePrefix = "Database.Migrations.";

    public async Task MigrateAsync(CancellationToken token = default)
    {
        await using var connection = await factory.OpenAsync(token);

        // WAL: читатели не блокируют писателя. Пишется в файл базы один раз и навсегда.
        await connection.ExecuteAsync("pragma journal_mode = wal;");

        await connection.ExecuteAsync(
            "create table if not exists schema_migrations (version integer primary key, applied_at text not null);");

        var applied = (await connection.QueryAsync<long>("select version from schema_migrations;"))
            .ToHashSet();

        foreach (var (version, name, sql) in LoadMigrations())
        {
            if (!applied.Add(version))
                continue;

            await using var transaction = await connection.BeginTransactionAsync(token);

            await connection.ExecuteAsync(sql, transaction: transaction);
            await connection.ExecuteAsync(
                "insert into schema_migrations (version, applied_at) values (@version, @appliedAt);",
                new { version, appliedAt = Iso.Format(DateTimeOffset.UtcNow) },
                transaction);

            await transaction.CommitAsync(token);

            logger.LogInformation("Применена миграция {Version} ({Name})", version, name);
        }
    }

    private static IEnumerable<(long Version, string Name, string Sql)> LoadMigrations()
    {
        var assembly = Assembly.GetExecutingAssembly();

        return assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                        && n.EndsWith(".sql", StringComparison.Ordinal))
            .Select(resource =>
            {
                var shortName = resource[ResourcePrefix.Length..];
                var underscore = shortName.IndexOf('_');

                if (underscore <= 0 || !long.TryParse(shortName[..underscore], out var version))
                {
                    throw new InvalidOperationException(
                        $"Миграция {shortName} названа неправильно: имя должно начинаться с номера и подчёркивания.");
                }

                using var stream = assembly.GetManifestResourceStream(resource)!;
                using var reader = new StreamReader(stream);

                return (version, shortName, reader.ReadToEnd());
            })
            .OrderBy(m => m.version)
            .ToArray();
    }
}
