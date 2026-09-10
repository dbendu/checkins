using CheckIn.Api.Auth;
using Database;
using Domain.Exceptions;
using Domain.Models;
using Logic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckIn.Api.Controllers.CheckIns;

[ApiController]
[Route("api/checkins")]
[Authorize]
public sealed class CheckInsController(
    CheckInService checkIns,
    IPlacesCategoriesRepository placesCategoriesRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CheckInResponse[]>> List(CancellationToken token)
    {
        var items = await checkIns.ListAsync(User.RequireUserId(), token);

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

    [HttpGet("map")]
    public async Task<ActionResult<VisitedPlaceResponse[]>> Map(CancellationToken token)
    {
        var visits = await checkIns.ListAllAsync(token);

        var response = visits
            .GroupBy(visit => visit.Place.Id)
            .Select(group =>
            {
                var place = group.First().Place;

                return new VisitedPlaceResponse(
                    place.Id,
                    place.Name,
                    place.Category.Title,
                    place.Address,
                    place.Location.Lat,
                    place.Location.Lon,
                    group
                        .OrderByDescending(visit => visit.CreatedAt)
                        .Select(visit => new VisitorResponse(
                            visit.Visitor.Id,
                            visit.Visitor.DisplayName,
                            visit.Visitor.HasPhoto,
                            visit.CreatedAt))
                        .ToArray()
                );
            })
            .ToArray();

        return Ok(response);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken token)
    {
        var deleted = await checkIns.DeleteAsync(User.RequireUserId(), id, token);

        return deleted ? NoContent() : NotFound();
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
            await checkIns.CreateAsync(User.RequireUserId(), place, token);
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
