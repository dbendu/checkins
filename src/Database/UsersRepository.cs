using Dapper;
using Domain.Models;

namespace Database;

public interface IUsersRepository
{
    Task<User?> FindAsync(long id, CancellationToken token);

    Task<UserCredentials?> FindByLoginAsync(string login, CancellationToken token);

    Task<User?> CreateAsync(
        string login, string displayName, string passwordHash, string passwordSalt,
        DateTimeOffset createdAt, CancellationToken token);
}

public sealed class UsersRepository(IDbConnectionFactory factory) : IUsersRepository
{
    public async Task<User?> FindAsync(long id, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        return await connection.QuerySingleOrDefaultAsync<User>(new CommandDefinition(
            "select id as Id, login as Login, display_name as DisplayName from users where id = @id;",
            new { id },
            cancellationToken: token));
    }

    public async Task<UserCredentials?> FindByLoginAsync(string login, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        return await connection.QuerySingleOrDefaultAsync<UserCredentials>(new CommandDefinition(
            """
            select id            as Id,
                   login         as Login,
                   display_name  as DisplayName,
                   password_hash as PasswordHash,
                   password_salt as PasswordSalt
            from users
            where login = @login collate nocase;
            """,
            new { login },
            cancellationToken: token));
    }

    public async Task<User?> CreateAsync(
        string login, string displayName, string passwordHash, string passwordSalt,
        DateTimeOffset createdAt, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        return await connection.QuerySingleOrDefaultAsync<User>(new CommandDefinition(
            """
            insert into users (login, display_name, password_hash, password_salt, created_at)
            values (@login, @displayName, @passwordHash, @passwordSalt, @createdAt)
            on conflict (login) do nothing
            returning id as Id, login as Login, display_name as DisplayName;
            """,
            new { login, displayName, passwordHash, passwordSalt, createdAt = Iso.Format(createdAt) },
            cancellationToken: token));
    }
}
