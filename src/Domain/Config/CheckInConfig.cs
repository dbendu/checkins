namespace Domain.Config;

public class CheckInConfig
{
    public const string Section = "CheckIn";

    public required int CooldownMinutes { get; init; }
}
