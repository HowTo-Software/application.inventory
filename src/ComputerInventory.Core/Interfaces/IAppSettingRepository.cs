using ComputerInventory.Core.Entities;

namespace ComputerInventory.Core.Interfaces;

public interface IAppSettingRepository
{
    Task<AppSetting?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default);
}
