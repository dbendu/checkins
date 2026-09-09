using System.Globalization;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Database;
using Domain.Exceptions;
using Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Overpass.Config;

namespace Overpass;

public interface IPlaceProvider
{
    Task<IReadOnlyCollection<Place>> FindNearbyAsync(
        GeoPoint center,
        int radiusMeters,
        IReadOnlyCollection<PlaceCategory> categories,
        CancellationToken token);
}

public sealed class OverpassPlaceProvider(
    HttpClient http,
    IOptions<OverpassConfig> overpassConfig,
    IPlacesCategoriesRepository placesCategoriesRepository,
    ILogger<OverpassPlaceProvider> logger) : IPlaceProvider
{
    private const string ProviderName = "Overpass";

    private readonly record struct Filter(string Key, string Value, string CategoryId);

    public async Task<IReadOnlyCollection<Place>> FindNearbyAsync(
        GeoPoint center,
        int radiusMeters,
        IReadOnlyCollection<PlaceCategory> categories,
        CancellationToken token)
    {
        var filters = MapFilters(categories).ToArray();

        var query = BuildQuery(center, radiusMeters, filters);

        try
        {
            using var response = await SendAsync(overpassConfig.Value.Client.Endpoint, query, token);

            await using var stream = await response.Content.ReadAsStreamAsync(token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);

            return await Parse(document, filters, token).ToListAsync(cancellationToken: token);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Ошибка при чтении тела ответа");
            throw new PlaceProviderException("Overpass вернул не JSON.", ex.Message);
        }
        catch (Exception ex) when (ex is not PlaceProviderException && !token.IsCancellationRequested)
        {
            logger.LogError(ex, "Ошибка при выполнении запроса");
            throw new PlaceProviderException("Ошибка при выполнении запроса", ex.Message);
        }
        catch (TaskCanceledException ex)
        {
            logger.LogError(ex, "Таймаут запроса");
            throw new PlaceProviderException("Таймаут запроса", ex.Message);
        }
    }

    private IEnumerable<Filter> MapFilters(IReadOnlyCollection<PlaceCategory> categories)
    {
        foreach (var category in categories)
        {
            var mapping = overpassConfig.Value.CategoriesMappings
                .FirstOrDefault(mapping => mapping.CategoryId == category.Id);

            if (mapping is null)
                throw new PlaceProviderException(ProviderName, $"Неподдерживаемая категория '{category.Id}'");

            // У одной категории тегов может быть несколько: «Бары» — это и amenity=bar,
            // и amenity=pub. Каждый становится отдельным условием в запросе, но помнит
            // свою категорию: иначе при разборе ответа не сказать, чем оказалась точка.
            foreach (var raw in mapping.Filters)
            {
                if (ParseFilter(raw, category.Id) is { } filter)
                    yield return filter;
            }
        }
    }

    private string BuildQuery(GeoPoint center, int radiusMeters, IEnumerable<Filter> filters)
    {
        var lat = center.Lat.ToString("R", CultureInfo.InvariantCulture);
        var lon = center.Lon.ToString("R", CultureInfo.InvariantCulture);

        var builder = new StringBuilder();

        builder.Append("[timeout:").Append(overpassConfig.Value.Client.RequestTimeoutSeconds).Append("]");
        builder.Append("[out:json]");
        builder.Append(';');

        builder.Append('(');

        foreach (var filter in filters)
        {
            builder.Append("node(around:")
                .Append(radiusMeters.ToString(CultureInfo.InvariantCulture))
                .Append(',').Append(lat).Append(',').Append(lon).Append(")[")
                .Append('"').Append(Escape(filter.Key)).Append('"')
                .Append('=')
                .Append('"').Append(Escape(filter.Value)).Append('"')
                .Append("];");
        }

        return builder.Append(");out body;").ToString();
    }

    private async Task<HttpResponseMessage> SendAsync(Uri endpoint, string query, CancellationToken ct)
    {
        var body = new FormUrlEncodedContent([new KeyValuePair<string, string>("data", query)]);

        var response = await http.PostAsync(endpoint, body, ct);
        if (response.IsSuccessStatusCode)
            return response;

        var status = response.StatusCode;
        response.Dispose();

        throw status is HttpStatusCode.TooManyRequests or HttpStatusCode.GatewayTimeout
            ? new PlaceProviderException(ProviderName, "Overpass сейчас перегружен, попробуйте через минуту.")
            : new PlaceProviderException(ProviderName, $"Overpass ответил {(int)status}.");
    }

    private async IAsyncEnumerable<Place> Parse(JsonDocument document, Filter[] filters, [EnumeratorCancellation] CancellationToken token)
    {
        if (!document.RootElement.TryGetProperty("elements", out var elements) ||
            elements.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        var categories = await placesCategoriesRepository.Get(token);

        foreach (var element in elements.EnumerateArray())
        {
            if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object)
                continue;

            // Точки без названия — шум: в списке показать нечего.
            if (!TryGetString(tags, "name", out var name)) continue;

            if (!TryGetDouble(element, "lat", out var lat) ||
                !TryGetDouble(element, "lon", out var lon))
            {
                continue;
            }

            var location = new GeoPoint(lat, lon);

            if (!element.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String) continue;
            if (!element.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number) continue;

            var category = MatchCategory(tags, filters, categories);

            yield return new Place(
                $"{type.GetString()}/{id.GetRawText()}",
                name,
                category,
                location,
                BuildAddress(tags)
            );
        }
    }

    /// <summary>Точка может подойти сразу под несколько фильтров -- берём первый совпавший.</summary>
    private PlaceCategory MatchCategory(JsonElement tags, Filter[] filters, PlaceCategory[] categories)
    {
        foreach (var filter in filters)
        {
            if (TryGetString(tags, filter.Key, out var value) &&
                string.Equals(value, filter.Value, StringComparison.OrdinalIgnoreCase))
            {
                return categories.First(category => category.Id == filter.CategoryId);
            }
        }

        throw new Exception();
    }

    private static string? BuildAddress(JsonElement tags)
    {
        TryGetString(tags, "addr:street", out var street);
        TryGetString(tags, "addr:housenumber", out var house);

        var parts = new[] { street, house }.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        return parts.Length > 0 ? string.Join(", ", parts) : null;
    }

    /// <summary>Разбирает "amenity=cafe" из конфига. Кривую строку пропускаем с предупреждением.</summary>
    private Filter? ParseFilter(string filter, string categoryId)
    {
        var separator = filter.IndexOf('=');
        if (separator > 0 && separator < filter.Length - 1)
            return new Filter(filter[..separator].Trim(), filter[(separator + 1)..].Trim(), categoryId);

        logger.LogWarning(
            "Фильтр {Filter} категории {Category} пропущен: ожидается вид ключ=значение",
            filter, categoryId);

        return null;
    }

    private static bool TryGetString(JsonElement element, string name, out string value)
    {
        value = "";

        if (element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.String &&
            property.GetString() is { Length: > 0 } text)
        {
            value = text;
            return true;
        }

        return false;
    }

    private static bool TryGetDouble(JsonElement element, string name, out double value)
    {
        value = 0;

        return element.TryGetProperty(name, out var property)
               && property.ValueKind == JsonValueKind.Number
               && property.TryGetDouble(out value);
    }

    // Кавычки и обратные слэши ломают синтаксис Overpass QL.
    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace("\"", "\\\"", StringComparison.Ordinal);
}