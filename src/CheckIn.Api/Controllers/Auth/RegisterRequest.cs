using System.ComponentModel.DataAnnotations;

namespace CheckIn.Api.Controllers.Auth;

public class RegisterRequest
{
    [MinLength(1)]
    public required string Login { get; init; }

    [MinLength(1)]
    public required string Password { get; init; }

    [MinLength(1)]
    public required string DisplayName { get; init; }
}