using Domain.Config;
using Domain.Exceptions;
using Domain.Models;
using Microsoft.Extensions.Options;
using Overpass;

namespace Logic;

public sealed class NearbyPlacesService(
    IPlaceProvider provider,
    IOptions<PlacesCategoriesConfig> categoriesConfig,
    IOptions<SearchConfig> searchConfig)
{
    public async Task<NearbySearchResult> FindAsync(
        GeoPoint center,
        IReadOnlyCollection<string>? selectedCategories,
        CancellationToken token)
    {
        var categories = SelectCategories(selectedCategories);

        var places = await provider.FindNearbyAsync(center, searchConfig.Value.RadiusMeters, categories, token);

        return new NearbySearchResult(
            searchConfig.Value.RadiusMeters,
            places
                .Select(place => new NearbyPlace(place, Geo.DistanceMeters(center, place.Location)))
                .Where(x => x.DistanceMeters <= searchConfig.Value.RadiusMeters)
                .OrderBy(x => x.DistanceMeters)
                .Take(searchConfig.Value.MaxResults)
                .ToArray()
        );
    }

    private IReadOnlyCollection<CategoryConfig> SelectCategories(IReadOnlyCollection<string>? selectedCategories)
    {
        if (selectedCategories?.Count is null or 0)
            return categoriesConfig.Value.Categories;

        var selected = new List<CategoryConfig>();

        foreach (var selectedCategory in selectedCategories.Distinct())
        {
            var category = categoriesConfig.Value.Categories
                .FirstOrDefault(category => category.Id.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));

            if (category is null)
                throw new UnknownCategoryException(selectedCategory);

            selected.Add(category);
        }

        return selected;
    }
}
