using Dapper;
using Domain.Models;

namespace Database;

public interface IUsersRepository
{
    Task<User> UpsertAsync(long telegramId, string displayName, string? username, string? photoUrl, CancellationToken token);

    Task<User?> FindAsync(long id, CancellationToken token);
}

public sealed class UsersRepository(IDbConnectionFactory factory) : IUsersRepository
{
    private const string Columns =
        "id as Id, telegram_id as TelegramId, display_name as DisplayName, " +
        "username as Username, photo_url as PhotoUrl";

    public async Task<User> UpsertAsync(
        long telegramId, string displayName, string? username, string? photoUrl, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        return await connection.QuerySingleAsync<User>(new CommandDefinition(
            $"""
             insert into users (telegram_id, display_name, username, photo_url)
             values (@telegramId, @displayName, @username, @photoUrl)
             on conflict (telegram_id) do update set
                 display_name = excluded.display_name,
                 username     = excluded.username,
                 photo_url    = excluded.photo_url
             returning {Columns};
             """,
            new { telegramId, displayName, username, photoUrl },
            cancellationToken: token));
    }

    public async Task<User?> FindAsync(long id, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        return await connection.QuerySingleOrDefaultAsync<User>(new CommandDefinition(
            $"select {Columns} from users where id = @id;",
            new { id },
            cancellationToken: token));
    }
}
