using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Domain.Config;
using Microsoft.Extensions.Options;

namespace Logic;

/// <summary>Что прислал виджет Telegram после успешного входа.</summary>
public sealed record TelegramUser(
    long Id,
    string? FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl)
{
    /// <summary>
    /// Как показывать человека. Имя с фамилией, если есть; иначе ник; иначе id --
    /// в Telegram фамилия и ник необязательны, а показать что-то надо всегда.
    /// </summary>
    public string DisplayName
    {
        get
        {
            var full = string.Join(' ', new[] { FirstName, LastName }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

            if (!string.IsNullOrWhiteSpace(full)) return full;
            if (!string.IsNullOrWhiteSpace(Username)) return Username;

            return $"id{Id}";
        }
    }
}

public sealed record TelegramLoginResult(TelegramUser? User, string? Error)
{
    public bool Ok => User is not null;

    public static TelegramLoginResult Fail(string error) => new(null, error);
    public static TelegramLoginResult Success(TelegramUser user) => new(user, null);
}

/// <summary>
/// Проверка подписи Telegram Login Widget по их документации:
///
/// строка проверки -- все поля кроме hash, отсортированные по имени и склеенные
/// как «ключ=значение» через перевод строки; секретный ключ — SHA256 от токена
/// бота; подпись -- HMAC-SHA256 от строки на этом ключе.
///
/// Сравнивать подписи обычным сравнением нельзя -- это утечка по времени, поэтому FixedTimeEquals.
/// </summary>
public sealed class TelegramLoginVerifier(IOptions<TelegramConfig> config, TimeProvider clock)
{
    public TelegramLoginResult Verify(IReadOnlyDictionary<string, string> fields)
    {
        var settings = config.Value;

        if (string.IsNullOrWhiteSpace(settings.BotToken))
            return TelegramLoginResult.Fail("Вход через Telegram не настроен на сервере.");

        if (!fields.TryGetValue("hash", out var hash) || string.IsNullOrWhiteSpace(hash))
            return TelegramLoginResult.Fail("В ответе Telegram нет подписи.");

        if (!fields.TryGetValue("auth_date", out var authDateRaw) ||
            !long.TryParse(authDateRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var authDate))
        {
            return TelegramLoginResult.Fail("В ответе Telegram нет корректного auth_date.");
        }

        if (!fields.TryGetValue("id", out var idRaw) ||
            !long.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
        {
            return TelegramLoginResult.Fail("В ответе Telegram нет корректного id пользователя.");
        }

        // Подписываем всё, что пришло, кроме самой подписи: Telegram может добавить
        // новое поле, и оно обязано попасть в строку проверки, иначе подпись не сойдётся.
        var checkString = string.Join('\n', fields
            .Where(field => field.Key != "hash")
            .OrderBy(field => field.Key, StringComparer.Ordinal)
            .Select(field => $"{field.Key}={field.Value}"));

        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(settings.BotToken));
        var expected = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(checkString));

        if (!TryParseHex(hash, expected.Length, out var actual) ||
            !CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return TelegramLoginResult.Fail("Подпись Telegram не сошлась.");
        }

        var age = clock.GetUtcNow() - DateTimeOffset.FromUnixTimeSeconds(authDate);

        if (age > TimeSpan.FromMinutes(settings.AuthMaxAgeMinutes))
            return TelegramLoginResult.Fail("Ответ Telegram слишком старый, войдите заново.");

        // Часы сервера могут немного отставать от телеграмовских -- небольшой запас вперёд.
        if (age < TimeSpan.FromMinutes(-5))
            return TelegramLoginResult.Fail("Ответ Telegram датирован будущим.");

        return TelegramLoginResult.Success(new TelegramUser(
            id,
            Get(fields, "first_name"),
            Get(fields, "last_name"),
            Get(fields, "username"),
            Get(fields, "photo_url")));
    }

    private static string? Get(IReadOnlyDictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static bool TryParseHex(string value, int expectedBytes, out byte[] bytes)
    {
        bytes = [];
        if (value.Length != expectedBytes * 2) return false;

        try
        {
            bytes = Convert.FromHexString(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
