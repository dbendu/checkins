using Database;
using Database.Config;
using Domain.Config;
using Logic;
using Microsoft.Extensions.Options;
using Overpass;
using Overpass.Config;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails();

builder.Services
    .AddOptions<SearchConfig>()
    .BindConfiguration(SearchConfig.Section)
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<OverpassConfig>()
    .BindConfiguration(OverpassConfig.Section)
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<CheckInConfig>()
    .BindConfiguration(CheckInConfig.Section)
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<DatabaseConfig>()
    .BindConfiguration(DatabaseConfig.Section)
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

builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddSingleton<Migrator>();
builder.Services.AddScoped<IPlacesRepository, PlacesRepository>();
builder.Services.AddScoped<ICheckInsRepository, CheckInsRepository>();
builder.Services.AddScoped<IPlacesCategoriesRepository, PlacesCategoriesRepository>();
builder.Services.AddScoped<CheckInService>();

var app = builder.Build();

await app.Services.GetRequiredService<Migrator>().MigrateAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Статика лежит в wwwroot и раздаётся как есть: ни сборки, ни зависимостей.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();

app.Run();
