using Database;
using Domain.Exceptions;
using Domain.Models;

namespace Logic;

public class UsersService(
    IUsersRepository users,
    TimeProvider clock)
{
    public async Task<User> CreateAsync(
        string login,
        string password,
        string displayName,
        CancellationToken token
    )
    {
        var (hash, salt) = PasswordHasher.Hash(password);

        var user = await users.CreateAsync(login, displayName, hash, salt, clock.GetUtcNow(), token);

        return user ?? throw new LoginTakenException();
    }

    public async Task<User> VerifyAsync(string login, string password, CancellationToken token)
    {
        var found = await users.FindByLoginAsync(login, token);

        if (found is null)
            throw new UserNotFoundException();

        if (!PasswordHasher.Verify(password, found.PasswordHash, found.PasswordSalt))
            throw new InvalidPasswordException();

        return new User(found.Id, found.Login, found.DisplayName);
    }
}
