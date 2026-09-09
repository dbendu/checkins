using Database;
using Domain.Exceptions;
using Domain.Models;
using Logic;
using Microsoft.AspNetCore.Mvc;

namespace CheckIn.Api.Controllers.CheckIns;

[ApiController]
[Route("api/checkins")]
public sealed class CheckInsController(
    CheckInService checkIns,
    IPlacesCategoriesRepository placesCategoriesRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CheckInResponse[]>> List(
        [FromQuery] int take, CancellationToken token)
    {
        var items = await checkIns.ListAsync(take, token);

        var response = items
            .Select(item => new CheckInResponse(
                    item.Id,
                    item.Place.Id,
                    item.Place.Name,
                    item.Place.Category.Title,
                    item.Place.Address,
                    item.Place.Location.Lat,
                    item.Place.Location.Lon,
                    item.CreatedAt
                )
            )
            .ToArray();

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateCheckInRequest request, CancellationToken token)
    {
        var place = new Place(
            request.PlaceId,
            request.Name,
            Category: await placesCategoriesRepository.Find(request.CategoryId, token),
            new GeoPoint(request.Lat, request.Lon),
            Address: request.Address
        );

        try
        {
            await checkIns.CreateAsync(place, token);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (CheckInTooSoonException e)
        {
            return Problem(
                detail: $"{e.Message} Следующая отметка — после {e.RetryAfter.ToLocalTime():HH:mm}.",
                statusCode: StatusCodes.Status409Conflict);
        }
    }
}
