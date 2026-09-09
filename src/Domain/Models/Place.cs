namespace Domain.Models;

public record Place(
    string Id,
    string Name,
    PlaceCategory Category,
    GeoPoint Location,
    string? Address
);
