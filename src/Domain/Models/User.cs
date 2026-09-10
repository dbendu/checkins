namespace Domain.Models;

public sealed record User(
    long Id,
    string Login,
    string DisplayName
);
