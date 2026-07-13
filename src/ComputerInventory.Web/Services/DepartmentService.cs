using ComputerInventory.Contracts;

namespace ComputerInventory.Web.Services;

public interface IDepartmentService : ILookupCrudService<DepartmentItemDto> { }

public class DepartmentService : LookupCrudService<DepartmentItemDto>, IDepartmentService
{
    public DepartmentService(IApiClient apiClient)
        : base(apiClient, ApiEndpoints.Departments.Base, ApiEndpoints.Departments.ById) { }
}
