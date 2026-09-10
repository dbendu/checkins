namespace CheckIn.Api.Controllers.CheckIns;

public sealed record VisitedPlaceResponse(
    string PlaceId,
    string Name,
    string Category,
    string? Address,
    double Lat,
    double Lon,
    VisitorResponse[] Visitors
);
