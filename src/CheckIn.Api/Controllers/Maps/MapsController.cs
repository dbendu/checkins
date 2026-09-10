using Domain.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CheckIn.Api.Controllers.Maps;

[ApiController]
[Route("api/maps")]
[Authorize]
public sealed class MapsController(IOptions<MapsConfig> config) : ControllerBase
{
    [HttpGet("config")]
    public MapsConfigResponse Get() => new(config.Value.ApiKey);
}
