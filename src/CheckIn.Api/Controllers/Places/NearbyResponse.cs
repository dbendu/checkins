namespace CheckIn.Api.Controllers.Places;

public sealed record NearbyResponse(
    int RadiusMeters,
    NearbyPlaceResponse[] Places
);

public sealed record NearbyPlaceResponse(
    string Id,
    string Name,
    string CategoryId,
    string Category,
    string? Address,
    double Lat,
    double Lon,
    int DistanceMeters
);
