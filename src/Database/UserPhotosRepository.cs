using Dapper;
using Domain.Models;

namespace Database;

public interface IUserPhotosRepository
{
    Task<UserPhoto?> FindAsync(long userId, CancellationToken token);

    Task<bool> ExistsAsync(long userId, CancellationToken token);

    Task SetAsync(long userId, string contentType, byte[] data, CancellationToken token);
}

public sealed class UserPhotosRepository(IDbConnectionFactory factory) : IUserPhotosRepository
{
    public async Task<UserPhoto?> FindAsync(long userId, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        return await connection.QuerySingleOrDefaultAsync<UserPhoto>(new CommandDefinition(
            "select content_type as ContentType, data as Data from user_photos where user_id = @userId;",
            new { userId },
            cancellationToken: token));
    }

    public async Task<bool> ExistsAsync(long userId, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        var found = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "select user_id from user_photos where user_id = @userId;",
            new { userId },
            cancellationToken: token));

        return found is not null;
    }

    public async Task SetAsync(long userId, string contentType, byte[] data, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into user_photos (user_id, content_type, data)
            values (@userId, @contentType, @data)
            on conflict (user_id) do update set
                content_type = excluded.content_type,
                data         = excluded.data;
            """,
            new { userId, contentType, data },
            cancellationToken: token));
    }
}
