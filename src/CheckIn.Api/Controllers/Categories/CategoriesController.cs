using Domain.Config;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CheckIn.Api.Controllers.Categories;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(IOptions<PlacesCategoriesConfig> config) : ControllerBase
{
    [HttpGet]
    public ActionResult<CategoryResponse[]> Get()
    {
        var response = config.Value.Categories
            .Select(category => new CategoryResponse(category.Id, category.Title))
            .ToArray();

        return Ok(response);
    }
}
