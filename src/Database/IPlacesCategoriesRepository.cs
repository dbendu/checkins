using Dapper;
using Domain.Models;

namespace Database;

public interface IPlacesCategoriesRepository
{
    Task<PlaceCategory[]> Get(CancellationToken token);

    Task<PlaceCategory> Find(string id, CancellationToken token);
}

public class PlacesCategoriesRepository(IDbConnectionFactory factory) : IPlacesCategoriesRepository
{
    public async Task<PlaceCategory[]> Get(CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        var categories = await connection.QueryAsync<PlaceCategory>(new CommandDefinition(
            """
            select
                id as Id,
                title as Title
            from places_categories
            """,
            cancellationToken: token));

        return categories.ToArray();
    }

    public async Task<PlaceCategory> Find(string id, CancellationToken token)
    {
        await using var connection = await factory.OpenAsync(token);

        var category = await connection.QuerySingleAsync<PlaceCategory>(new CommandDefinition(
            """
            select
                id as Id,
                title as Title
            from places_categories
            where id = @Id
            """,
            new
            {
                Id = id
            },
            cancellationToken: token));

        return category;
    }
}