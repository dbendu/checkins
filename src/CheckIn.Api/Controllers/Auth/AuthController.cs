using System.Security.Claims;
using CheckIn.Api.Auth;
using Database;
using Domain.Exceptions;
using Domain.Models;
using Logic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace CheckIn.Api.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IUsersRepository users,
    IUserPhotosRepository photos,
    UsersService accounts) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(
        [FromBody] RegisterRequest request,
        CancellationToken token)
    {
        try
        {
            var user = await accounts.CreateAsync(
                request.Login,
                request.Password,
                request.DisplayName,
                token
            );

            await SignInAsync(user);

            return Created(string.Empty, await ToResponseAsync(user, token));
        }
        catch (LoginTakenException)
        {
            return Problem(detail: "Логин занят", statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken token)
    {
        try
        {
            var user = await accounts.VerifyAsync(request.Login, request.Password, token);

            await SignInAsync(user);

            return Ok(await ToResponseAsync(user, token));
        }
        catch (UserNotFoundException)
        {
            return Problem(
                detail: "Пользователь не найден",
                statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (InvalidPasswordException)
        {
            return Problem(
                detail: "Неправильный пароль",
                statusCode: StatusCodes.Status401Unauthorized);
        }
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken token)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var user = await users.FindAsync(userId.Value, token);

        // Cookie ещё жива, а пользователя уже нет — например, базу пересоздали.
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized();
        }

        return Ok(await ToResponseAsync(user, token));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return NoContent();
    }

    private async Task SignInAsync(User user)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private async Task<UserResponse> ToResponseAsync(User user, CancellationToken token) =>
        new(user.Id, user.Login, user.DisplayName, await photos.ExistsAsync(user.Id, token));
}
