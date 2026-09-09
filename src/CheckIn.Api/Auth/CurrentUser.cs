using System.Globalization;
using System.Security.Claims;

namespace CheckIn.Api.Auth;

public static class CurrentUser
{
    /// <summary>
    /// В cookie лежит только наш внутренний id: имя и аватар читаются из базы,
    /// чтобы смена профиля в Telegram была видна сразу, а не после перелогина.
    /// </summary>
    public static long? UserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
            ? id
            : null;
    }

    public static long RequireUserId(this ClaimsPrincipal principal) =>
        principal.UserId() ?? throw new InvalidOperationException(
            "Обработчик требует [Authorize], но в cookie нет id пользователя.");
}
