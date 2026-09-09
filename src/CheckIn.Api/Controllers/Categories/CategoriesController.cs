using Database;
using Microsoft.AspNetCore.Mvc;

namespace CheckIn.Api.Controllers.Categories;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(IPlacesCategoriesRepository repository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CategoryResponse[]>> Get(CancellationToken token)
    {
        var categories = await repository.Get(token);

        var response = categories
            .Select(category => new CategoryResponse(category.Id, category.Title))
            .ToArray();

        return Ok(response);
    }
}
