using Dapper;
using Domain.Models;

namespace Database;

public interface IPlacesRepository
{
    Task<long> UpsertAsync(Place place, DateTimeOffset updatedAt, CancellationToken token);
}

public sealed class PlacesRepository(IDbConnectionFactory factory) : IPlacesRepository
{
    public async Task<long> UpsertAsync(Place place, DateTimeOffset updatedAt, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        return await connection.QuerySingleAsync<long>(new CommandDefinition(
            """
            insert into places (external_id, name, category_id, lat, lon, address, updated_at)
            values (@externalId, @name, @categoryId, @lat, @lon, @address, @updatedAt)
            on conflict (external_id) do update set
                name        = excluded.name,
                category_id = excluded.category_id,
                lat         = excluded.lat,
                lon         = excluded.lon,
                address     = excluded.address,
                updated_at  = excluded.updated_at
            returning id;
            """,
            new
            {
                externalId = place.Id,
                name = place.Name,
                categoryId = place.Category.Id,
                lat = place.Location.Lat,
                lon = place.Location.Lon,
                address = place.Address,
                updatedAt = Iso.Format(updatedAt),
            },
            cancellationToken: token));
    }
}
