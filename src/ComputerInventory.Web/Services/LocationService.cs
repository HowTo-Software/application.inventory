using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface ILocationService : ILookupCrudService<LocationItemDto> { }

public class LocationService : LookupCrudService<LocationItemDto>, ILocationService
{
    public LocationService(IApiClient apiClient)
        : base(apiClient, ApiEndpoints.Locations.Base, ApiEndpoints.Locations.ById) { }
}
