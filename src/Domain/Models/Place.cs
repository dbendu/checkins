using Domain.Config;

namespace Domain.Models;

public record Place(
    string Id,
    string Name,
    CategoryConfig Category,
    GeoPoint Location,
    string? Address
);
