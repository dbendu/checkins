namespace CheckIn.Api.Controllers.CheckIns;

public sealed record CreateCheckInRequest
{
    public required string PlaceId { get; init; }

    public required string Name { get; init; }

    public required string CategoryId { get; init; }

    public required double Lat { get; init; }

    public required double Lon { get; init; }

    public string? Address { get; init; }
}
