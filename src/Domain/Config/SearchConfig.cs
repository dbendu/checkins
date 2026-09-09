namespace Domain.Config;

public class SearchConfig
{
    public const string Section = "Search";

    public required int RadiusMeters { get; init; }

    public required int MaxResults { get; init; }
}