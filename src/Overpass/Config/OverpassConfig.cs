namespace Overpass.Config;

public class OverpassConfig
{
    public const string Section = "Overpass";

    public required OverpassClientConfig Client { get; init; }

    public required OverpassPlaceCategoryMapping[] CategoriesMappings { get; init; }
}

public class OverpassClientConfig
{
    public required Uri Endpoint { get; init; }

    public required int RequestTimeoutSeconds { get; init; }
}

public class OverpassPlaceCategoryMapping
{
    public required string CategoryId { get; init; }

    public required string[] Filters { get; init; }
}