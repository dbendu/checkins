using Domain.Config;
using Logic;
using Microsoft.Extensions.Options;
using Overpass;
using Overpass.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails();

builder.Services
    .AddOptions<PlacesCategoriesConfig>()
    .BindConfiguration(PlacesCategoriesConfig.Section)
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<SearchConfig>()
    .BindConfiguration(SearchConfig.Section)
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<OverpassConfig>()
    .BindConfiguration(OverpassConfig.Section)
    .ValidateDataAnnotations();

// ---

builder.Services.AddHttpClient<IPlaceProvider, OverpassPlaceProvider>((sp, client) =>
{
    var settings = sp.GetRequiredService<IOptions<OverpassConfig>>().Value.Client;

    client.Timeout = TimeSpan.FromSeconds(settings.RequestTimeoutSeconds);
    // Overpass просит представляться: по User-Agent они разбирают, кто их грузит.
    client.DefaultRequestHeaders.UserAgent.ParseAdd("checkin-app/1.0");
});

builder.Services.AddScoped<NearbyPlacesService>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Статика лежит в wwwroot и раздаётся как есть: ни сборки, ни зависимостей.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
