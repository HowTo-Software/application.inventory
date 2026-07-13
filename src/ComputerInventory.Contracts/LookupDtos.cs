namespace ComputerInventory.Contracts;

public interface ILookupItemDto
{
    int Id { get; set; }
    string Name { get; set; }
    int UsageCount { get; set; }
}

public class DepartmentItemDto : ILookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public class LocationItemDto : ILookupItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}
