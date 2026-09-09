namespace Domain.Models;

public record NearbySearchResult(
    int RadiusMeters,
    NearbyPlace[] Places
);
