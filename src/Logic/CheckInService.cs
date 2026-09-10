using Database;
using Domain.Config;
using Domain.Exceptions;
using Domain.Models;
using Microsoft.Extensions.Options;

namespace Logic;

public sealed class CheckInService(
    IPlacesRepository places,
    ICheckInsRepository checkIns,
    IOptions<CheckInConfig> config)
{
    public Task<UserCheckIn[]> ListAsync(long userId, CancellationToken token) => checkIns.ListByUserAsync(userId, token);

    public Task<PlaceVisit[]> ListAllAsync(CancellationToken token) => checkIns.ListAllAsync(token);

    public async Task CreateAsync(long userId, Place place, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;

        // Снимок места обновляем до проверки кулдауна: человек его только что видел
        // в выдаче, значит данные свежие, и записать их полезно независимо от того,
        // разрешим мы отметку или нет.
        var placeId = await places.UpsertAsync(place, now, token);

        var lastCreatedAt = await checkIns.LastCreatedAtAsync(userId, placeId, token);
        var cooldown = TimeSpan.FromMinutes(config.Value.CooldownMinutes);

        if (lastCreatedAt is not null && now - lastCreatedAt.Value < cooldown)
            throw new CheckInTooSoonException(lastCreatedAt.Value + cooldown);

        await checkIns.AddAsync(userId, placeId, now, token);
    }
}
