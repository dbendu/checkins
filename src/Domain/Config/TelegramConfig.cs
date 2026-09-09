namespace Domain.Config;

public class TelegramConfig
{
    public const string Section = "Telegram";

    /// <summary>Имя бота без @. Нужно фронту, чтобы отрисовать виджет входа.</summary>
    public required string BotUsername { get; init; }

    /// <summary>
    /// Токен от @BotFather. В appsettings его нет: только user-secrets локально
    /// и переменная окружения Telegram__BotToken на сервере.
    /// </summary>
    public required string BotToken { get; init; }

    /// <summary>
    /// Насколько старым может быть auth_date. Подпись Telegram бессрочна, поэтому
    /// без этой проверки перехваченный ответ виджета работал бы как вечный пароль.
    /// </summary>
    public required int AuthMaxAgeMinutes { get; init; }

    /// <summary>
    /// Вход без Telegram для локальной разработки. Виджет требует настоящий домен
    /// (/setdomain у бота), поэтому на localhost войти иначе нельзя.
    /// Работает только вместе с окружением Development -- см. AuthController.
    /// </summary>
    public bool DevLogin { get; init; }
}
