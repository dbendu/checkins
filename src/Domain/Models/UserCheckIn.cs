namespace Domain.Models;

public sealed record UserCheckIn(
    long Id,
    Place Place,
    DateTimeOffset CreatedAt
);
