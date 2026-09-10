using CheckIn.Api.Auth;
using Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckIn.Api.Controllers.Users;

[ApiController]
[Route("api/users")]
public sealed class UsersController(IUserPhotosRepository photos, TimeProvider clock) : ControllerBase
{
    private const int MaxPhotoBytes = 2 * 1024 * 1024;

    private static readonly string[] AllowedTypes = ["image/jpeg", "image/png", "image/webp"];

    [HttpGet("{id:long}/photo")]
    public async Task<IActionResult> Photo(long id, CancellationToken token)
    {
        var photo = await photos.FindAsync(id, token);
        if (photo is null) return NotFound();

        return File(photo.Data, photo.ContentType);
    }

    [HttpPut("me/photo")]
    [Authorize]
    [RequestSizeLimit(MaxPhotoBytes + 4096)]
    public async Task<IActionResult> SetPhoto(IFormFile file, CancellationToken token)
    {
        if (file.Length == 0)
            return Problem(detail: "Файл пустой.", statusCode: StatusCodes.Status400BadRequest);

        if (file.Length > MaxPhotoBytes)
        {
            return Problem(
                detail: $"Файл больше {MaxPhotoBytes / 1024 / 1024} МБ.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var contentType = file.ContentType.Trim().ToLowerInvariant();

        if (!AllowedTypes.Contains(contentType))
        {
            return Problem(
                detail: $"Поддерживаются только {string.Join(", ", AllowedTypes)}.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, token);

        await photos.SetAsync(User.RequireUserId(), contentType, buffer.ToArray(), token);

        return NoContent();
    }
}
