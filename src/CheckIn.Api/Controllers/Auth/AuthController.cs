using System.Security.Claims;
using System.Text.Json;
using CheckIn.Api.Auth;
using Database;
using Domain.Config;
using Domain.Models;
using Logic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CheckIn.Api.Controllers.Auth;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    TelegramLoginVerifier verifier,
    IUsersRepository users,
    IOptions<TelegramConfig> config,
    IWebHostEnvironment environment) : ControllerBase
{
    private const long DevTelegramId = -1;

    private TelegramConfig Settings => config.Value;

    private bool DevLoginAllowed => environment.IsDevelopment() && Settings.DevLogin;

    [HttpGet("config")]
    public ActionResult<AuthConfigResponse> Config() =>
        Ok(new AuthConfigResponse(Settings.BotUsername, DevLoginAllowed));

    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken token)
    {
        var userId = User.UserId();
        if (userId is null) return Unauthorized();

        var user = await users.FindAsync(userId.Value, token);

        // Cookie ещё жива, а пользователя уже нет -- например, базу пересоздали.
        if (user is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Unauthorized();
        }

        return Ok(ToResponse(user));
    }

    /// <summary>
    /// Принимает то, что виджет Telegram отдал в колбэк. Тело — сырой объект:
    /// подписаны все поля, включая те, о которых мы ещё не знаем, поэтому
    /// фиксированной DTO здесь быть не может.
    /// </summary>
    [HttpPost("telegram")]
    public async Task<ActionResult<UserResponse>> Telegram(
        [FromBody] Dictionary<string, JsonElement> payload, CancellationToken token)
    {
        if (!TryReadFields(payload, out var fields))
        {
            return Problem(
                detail: "Тело запроса не является корректным UTF-8.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = verifier.Verify(fields);
        if (!result.Ok)
            return Problem(detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);

        var telegramUser = result.User!;

        var user = await users.UpsertAsync(
            telegramUser.Id,
            telegramUser.DisplayName,
            telegramUser.Username,
            telegramUser.PhotoUrl,
            token);

        await SignInAsync(user);

        return Ok(ToResponse(user));
    }

    /// <summary>
    /// Вход без Telegram — только для локальной разработки. Виджет требует настоящий
    /// домен, на localhost войти иначе невозможно. Вне Development ручки не существует.
    /// </summary>
    [HttpPost("dev-login")]
    public async Task<ActionResult<UserResponse>> DevLogin(CancellationToken token)
    {
        if (!DevLoginAllowed) return NotFound();

        var user = await users.UpsertAsync(
            DevTelegramId, "Локальный разработчик", username: null, photoUrl: null, token);

        await SignInAsync(user);

        return Ok(ToResponse(user));
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

    /// <summary>
    /// Раскладывает объект от виджета в плоские строки для проверки подписи.
    ///
    /// JSON обязан быть в UTF-8, но прислать могут что угодно, а System.Text.Json
    /// декодирует строки лениво -- битые байты всплыли бы здесь необработанным
    /// исключением, то есть человек получил бы 500 вместо честного 400.
    /// </summary>
    private static bool TryReadFields(
        Dictionary<string, JsonElement> payload, out Dictionary<string, string> fields)
    {
        fields = new Dictionary<string, string>(StringComparer.Ordinal);

        try
        {
            foreach (var (key, value) in payload)
            {
                var text = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
                    _ => null,
                };

                if (text is not null) fields[key] = text;
            }
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return true;
    }

    private static UserResponse ToResponse(User user) =>
        new(user.DisplayName, user.Username, user.PhotoUrl);
}
