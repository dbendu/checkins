namespace Domain.Models;

public sealed record PlaceVisit(
    Place Place,
    Visitor Visitor,
    DateTimeOffset CreatedAt
);
