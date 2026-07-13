namespace ComputerInventory.Api.Services;

/// <summary>
/// Configuration for agent file paths. Bound from "AgentFiles" section in appsettings.json.
/// All paths are relative to the application base directory unless absolute.
/// </summary>
public class AgentFileSettings
{
    public const string SectionName = "AgentFiles";

    public string AgentDirectory { get; set; } = "agent";
    public string UpdateSubdirectory { get; set; } = "update";
    public string PackagesSubdirectory { get; set; } = "packages";
    public string VersionFileName { get; set; } = "agent-version.txt";
    public string UpdateJsonFileName { get; set; } = "update.json";
    public string ChecksumFileName { get; set; } = "checksum.txt";
    public string InstallerFileName { get; set; } = "ComputerInventoryAgent.zip";
    public string UpdatePackageFileName { get; set; } = "agent-update.zip";

    /// <summary>Resolves the agent root directory as an absolute path.</summary>
    public string GetAgentPath() =>
        Path.IsPathRooted(AgentDirectory)
            ? AgentDirectory
            : Path.Combine(AppContext.BaseDirectory, AgentDirectory);

    /// <summary>Resolves the update directory (agent/update).</summary>
    public string GetUpdatePath() =>
        Path.Combine(GetAgentPath(), UpdateSubdirectory);

    /// <summary>Resolves the packages directory (agent/packages).</summary>
    public string GetPackagesPath() =>
        Path.Combine(GetAgentPath(), PackagesSubdirectory);

    /// <summary>Resolves the full path to agent-version.txt.</summary>
    public string GetVersionFilePath() =>
        Path.Combine(GetAgentPath(), VersionFileName);

    /// <summary>Resolves the full path to update/update.json.</summary>
    public string GetUpdateJsonPath() =>
        Path.Combine(GetUpdatePath(), UpdateJsonFileName);

    /// <summary>Resolves the full path to checksum.txt.</summary>
    public string GetChecksumFilePath() =>
        Path.Combine(GetAgentPath(), ChecksumFileName);

    /// <summary>Resolves the full path to the installer zip.</summary>
    public string GetInstallerPath() =>
        Path.Combine(GetAgentPath(), InstallerFileName);

    /// <summary>Resolves the full path to the update package zip.</summary>
    public string GetUpdatePackagePath() =>
        Path.Combine(GetUpdatePath(), UpdatePackageFileName);
}
