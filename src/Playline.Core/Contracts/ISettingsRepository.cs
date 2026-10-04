using Playline.Core.Models;

namespace Playline.Core.Contracts;

public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default);
}

