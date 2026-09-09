namespace Domain.Config;

public class PlacesCategoriesConfig
{
    public const string Section = "PlacesCategories";

    public required CategoryConfig[] Categories { get; init; }
}

public class CategoryConfig
{
    public required string Id { get; init; }

    public required string Title { get; init; }
}