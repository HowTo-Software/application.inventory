namespace ComputerInventory.Core.Interfaces;

public interface IHealthCheckRepository
{
    Task<int> GetHealthCheckCountAsync(CancellationToken cancellationToken = default);
    string GetProviderName();
}
