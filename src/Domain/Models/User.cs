namespace Domain.Models;

public sealed record User(
    long Id,
    long TelegramId,
    string DisplayName,
    string? Username,
    string? PhotoUrl
);
