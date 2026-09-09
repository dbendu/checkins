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
    private const long FakeUserId = 1;

    public Task<UserCheckIn[]> ListAsync(int take, CancellationToken token) =>
        checkIns.ListByUserAsync(FakeUserId, take, token);

    public async Task CreateAsync(Place place, CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow;

        // Снимок места обновляем до проверки кулдауна: человек его только что видел
        // в выдаче, значит данные свежие, и записать их полезно независимо от того,
        // разрешим мы отметку или нет.
        var placeId = await places.UpsertAsync(place, now, token);

        var lastCreatedAt = await checkIns.LastCreatedAtAsync(FakeUserId, placeId, token);
        var cooldown = TimeSpan.FromMinutes(config.Value.CooldownMinutes);

        if (lastCreatedAt is not null && now - lastCreatedAt.Value < cooldown)
            throw new CheckInTooSoonException(lastCreatedAt.Value + cooldown);

        await checkIns.AddAsync(FakeUserId, placeId, now, token);
    }
}
