namespace CheckIn.Api.Controllers.CheckIns;

public sealed record CheckInResponse(
    long Id,
    string PlaceId,
    string Name,
    string Category,
    string? Address,
    double Lat,
    double Lon,
    DateTimeOffset CreatedAt
);
