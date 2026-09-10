using Domain.Exceptions;
using Domain.Models;
using Logic;
using Microsoft.AspNetCore.Mvc;

namespace CheckIn.Api.Controllers.Places;

[ApiController]
[Route("api/places")]
public sealed class PlacesController(
    NearbyPlacesService search,
    ILogger<PlacesController> logger) : ControllerBase
{
    [HttpGet("nearby")]
    public async Task<ActionResult<NearbyResponse>> Nearby(
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery(Name = "category")] string[]? categories,
        CancellationToken token)
    {
        var center = new GeoPoint(lat, lon);

        try
        {
            var searchResult = await search.FindAsync(center, categories, token);

            var response = new NearbyResponse(
                searchResult.RadiusMeters,
                searchResult.Places
                    .Select(place =>
                        new NearbyPlaceResponse(
                            place.Place.Id,
                            place.Place.Name,
                            place.Place.Category.Id,
                            place.Place.Category.Title,
                            place.Place.Address,
                            place.Place.Location.Lat,
                            place.Place.Location.Lon,
                            (int)Math.Round(place.DistanceMeters)
                        )
                    )
                    .ToArray()
            );

            return Ok(response);
        }
        catch (UnknownCategoryException e)
        {
            return BadRequest(e.Message);
        }
        catch (PlaceProviderException e)
        {
            logger.LogWarning(e, "Поиск мест не удался, провайдер {Provider}", e.Provider);

            return Problem(
                title: "Справочник мест недоступен",
                detail: "OSM недоступны, повторите позже.",
                statusCode: StatusCodes.Status502BadGateway
            );
        }
    }
}
