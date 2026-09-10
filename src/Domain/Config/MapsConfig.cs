namespace Domain.Config;

public class MapsConfig
{
    public const string Section = "Maps";

    public required string ApiKey { get; init; }
}
