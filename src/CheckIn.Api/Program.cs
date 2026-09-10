using Database;
using Database.Config;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
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
    .AddOptions<MapsConfig>()
    .BindConfiguration(MapsConfig.Section)
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<DatabaseConfig>()
    .BindConfiguration(DatabaseConfig.Section)
    .ValidateDataAnnotations();

// Ключами Data Protection подписывается cookie. По умолчанию они живут в памяти
// процесса и умирают вместе с ним -- каждый перезапуск разлогинивал бы всех.
builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(Directory.CreateDirectory(builder.Configuration["DataProtection:KeysPath"]!))
    .SetApplicationName("checkin");

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "checkin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest // на localhost https нет
            : CookieSecurePolicy.Always;

        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

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
builder.Services.AddScoped<IUsersRepository, UsersRepository>();
builder.Services.AddScoped<IUserPhotosRepository, UserPhotosRepository>();
builder.Services.AddScoped<UsersService>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

await app.Services.GetRequiredService<Migrator>().MigrateAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Статика лежит в wwwroot и раздаётся как есть: ни сборки, ни зависимостей.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
