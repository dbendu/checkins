using Database;
using Domain.Config;
using Domain.Exceptions;
using Domain.Models;
using Microsoft.Extensions.Options;
using Overpass;

namespace Logic;

public sealed class NearbyPlacesService(
    IPlaceProvider provider,
    IPlacesCategoriesRepository placesCategoriesRepository,
    IOptions<SearchConfig> searchConfig)
{
    public async Task<NearbySearchResult> FindAsync(
        GeoPoint center,
        IReadOnlyCollection<string>? selectedCategories,
        CancellationToken token)
    {
        var categories = await SelectCategories(selectedCategories, token);

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

    private async Task<IReadOnlyCollection<PlaceCategory>> SelectCategories(
        IReadOnlyCollection<string>? selectedCategories,
        CancellationToken token)
    {
        var categories = await placesCategoriesRepository.Get(token);

        if (selectedCategories?.Count is null or 0)
            return categories;

        var selected = new List<PlaceCategory>();

        foreach (var selectedCategory in selectedCategories.Distinct())
        {
            var category = categories
                .FirstOrDefault(category => category.Id.Equals(selectedCategory, StringComparison.OrdinalIgnoreCase));

            if (category is null)
                throw new UnknownCategoryException(selectedCategory);

            selected.Add(category);
        }

        return selected;
    }
}
