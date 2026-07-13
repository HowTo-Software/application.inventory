using ComputerInventory.Core.Entities;
using ComputerInventory.Core.Interfaces;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Resolves string values (from requests/agents) to FK IDs in reference/lookup tables.
/// Uses find-or-create pattern: if a value doesn't exist, it's created automatically.
/// </summary>
public class ReferenceDataResolver
{
    private readonly IReferenceDataRepository _referenceDataRepository;

    // Per-request caches (safe because this service is Scoped — one instance per HTTP request)
    private readonly Dictionary<(string Manufacturer, string Model), int> _modelCache = new(StringTupleComparer.Instance);
    private readonly Dictionary<string, int> _osCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _departmentCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _locationCache = new(StringComparer.OrdinalIgnoreCase);

    public ReferenceDataResolver(IReferenceDataRepository referenceDataRepository)
    {
        _referenceDataRepository = referenceDataRepository;
    }

    /// <summary>
    /// Resolves manufacturer + model strings to a ComputerModel ID.
    /// Creates both Manufacturer and ComputerModel if they don't exist.
    /// Results are cached for the lifetime of the request.
    /// </summary>
    public async Task<int> ResolveModelIdAsync(string manufacturer, string model, CancellationToken ct = default)
    {
        var key = (manufacturer, model);
        if (_modelCache.TryGetValue(key, out var cachedId))
            return cachedId;

        // Atomic find-or-create via MERGE (race-condition safe)
        var mfrId = await _referenceDataRepository.FindOrCreateManufacturerAsync(manufacturer, ct) ?? 0;

        var modelId = await _referenceDataRepository.FindOrCreateComputerModelAsync(mfrId, model, ct) ?? 0;

        _modelCache[key] = modelId;
        return modelId;
    }

    /// <summary>
    /// Resolves an operating system string to an OperatingSystemEntity ID.
    /// Creates it if it doesn't exist.
    /// Results are cached for the lifetime of the request.
    /// </summary>
    public async Task<int?> ResolveOperatingSystemIdAsync(string? osName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(osName)) return null;

        if (_osCache.TryGetValue(osName, out var cachedId))
            return cachedId;

        // Atomic find-or-create via MERGE (race-condition safe)
        var osId = await _referenceDataRepository.FindOrCreateOperatingSystemAsync(osName, ct) ?? 0;

        _osCache[osName] = osId;
        return osId;
    }

    /// <summary>
    /// Resolves a department string to a Department ID.
    /// Creates it if it doesn't exist.
    /// Results are cached for the lifetime of the request.
    /// </summary>
    public async Task<int?> ResolveDepartmentIdAsync(string? department, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(department)) return null;

        if (_departmentCache.TryGetValue(department, out var cachedId))
            return cachedId;

        // Atomic find-or-create via MERGE (race-condition safe)
        var deptId = await _referenceDataRepository.FindOrCreateDepartmentAsync(department, ct) ?? 0;

        _departmentCache[department] = deptId;
        return deptId;
    }

    /// <summary>
    /// Resolves a location string to a Location ID.
    /// Creates it if it doesn't exist.
    /// Results are cached for the lifetime of the request.
    /// </summary>
    public async Task<int?> ResolveLocationIdAsync(string? location, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;

        if (_locationCache.TryGetValue(location, out var cachedId))
            return cachedId;

        // Atomic find-or-create via MERGE (race-condition safe)
        var locId = await _referenceDataRepository.FindOrCreateLocationAsync(location, ct) ?? 0;

        _locationCache[location] = locId;
        return locId;
    }

    /// <summary>
    /// Resolves a computer type string to its lookup ID.
    /// Falls back to ComputerType.Desktop if unrecognized.
    /// </summary>
    public ComputerType ResolveTypeId(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName)) return ComputerType.Desktop;
        return Enum.TryParse<ComputerType>(typeName, true, out var t) ? t : ComputerType.Desktop;
    }

    /// <summary>
    /// Case-insensitive comparer for (Manufacturer, Model) tuple keys.
    /// </summary>
    private sealed class StringTupleComparer : IEqualityComparer<(string, string)>
    {
        public static readonly StringTupleComparer Instance = new();
        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string, string) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
    }
}
