namespace CheckIn.Api.Controllers.Auth;

public sealed record UserResponse(
    string DisplayName,
    string? Username,
    string? PhotoUrl
);

public sealed record AuthConfigResponse(
    string BotUsername,
    bool DevLoginAvailable
);
