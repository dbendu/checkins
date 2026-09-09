using Dapper;
using Domain.Models;

namespace Database;

public interface ICheckInsRepository
{
    Task<DateTimeOffset?> LastCreatedAtAsync(long userId, long placeId, CancellationToken token);

    Task AddAsync(long userId, long placeId, DateTimeOffset createdAt, CancellationToken token);

    Task<UserCheckIn[]> ListByUserAsync(long userId, int take, CancellationToken token);
}

public sealed class CheckInsRepository(IDbConnectionFactory factory) : ICheckInsRepository
{
    private sealed record Row(
        long Id,
        string CreatedAt,
        string ExternalId,
        string Name,
        string CategoryId,
        string CategoryTitle,
        double Lat,
        double Lon,
        string? Address);

    public async Task<UserCheckIn[]> ListByUserAsync(long userId, int take, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            select c.id          as Id,
                   c.created_at  as CreatedAt,
                   p.external_id as ExternalId,
                   p.name        as Name,
                   p.category_id as CategoryId,
                   pc.title      as CategoryTitle,
                   p.lat         as Lat,
                   p.lon         as Lon,
                   p.address     as Address
            from check_ins c
                     join places p on p.id = c.place_id
                     join places_categories pc on pc.id = p.category_id
            where c.user_id = @userId
            order by c.created_at desc
            limit @take;
            """,
            new { userId, take },
            cancellationToken: token));

        return rows
            .Select(row => new UserCheckIn(
                row.Id,
                new Place(
                    row.ExternalId,
                    row.Name,
                    new PlaceCategory { Id = row.CategoryId, Title = row.CategoryTitle },
                    new GeoPoint(row.Lat, row.Lon),
                    row.Address),
                Iso.Parse(row.CreatedAt)))
            .ToArray();
    }

    public async Task<DateTimeOffset?> LastCreatedAtAsync(
        long userId, long placeId, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        var createdAt = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            """
            select created_at from check_ins
            where user_id = @userId and place_id = @placeId
            order by created_at desc
            limit 1;
            """,
            new { userId, placeId },
            cancellationToken: token));

        return createdAt is null ? null : Iso.Parse(createdAt);
    }

    public async Task AddAsync(long userId, long placeId, DateTimeOffset createdAt, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        await connection.ExecuteAsync(new CommandDefinition(
            """
            insert into check_ins (place_id, user_id, created_at)
            values (@placeId, @userId, @createdAt);
            """,
            new { placeId, userId, createdAt = Iso.Format(createdAt) },
            cancellationToken: token));
    }
}
