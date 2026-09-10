namespace CheckIn.Api.Controllers.Auth;

public sealed record UserResponse(long Id, string Login, string DisplayName, bool HasPhoto);
