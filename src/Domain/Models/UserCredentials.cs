namespace Domain.Models;

public sealed record UserCredentials(
    long Id,
    string Login,
    string DisplayName,
    string PasswordHash,
    string PasswordSalt
);
