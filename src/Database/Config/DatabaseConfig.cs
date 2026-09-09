namespace Database.Config;

public class DatabaseConfig
{
    public const string Section = "Database";

    public required string Path { get; init; }
}
