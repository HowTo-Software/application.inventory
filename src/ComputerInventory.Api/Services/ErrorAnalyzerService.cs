using ComputerInventory.Contracts;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace ComputerInventory.Api.Services;

/// <summary>
/// Analyzes raw agent error logs and produces structured error analysis
/// with categorized errors and remediation suggestions.
/// </summary>
public interface IErrorAnalyzerService
{
    /// <summary>
    /// Analyze raw error log text and return structured analysis with remediation steps.
    /// </summary>
    ErrorAnalysisResult Analyze(int computerId, string rawErrorLogs, AgentUpdateStatus lastStatus);
}

public class ErrorAnalyzerService : IErrorAnalyzerService
{
    private readonly ILogger<ErrorAnalyzerService> _logger;

    // Pre-compiled regex patterns for error detection
    private static readonly ErrorPattern[] ErrorPatterns = new[]
    {
        // Network errors
        new ErrorPattern(
            "Network",
            new Regex(@"(HttpRequestException|SocketException|No such host|connection was forcibly closed|network.*(unreachable|timeout)|DNS resolution failed|Could not resolve host|Connection refused|ConnectFailure|ReceiveFailure|SendFailure)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: true),

        new ErrorPattern(
            "Network",
            new Regex(@"(SSL|TLS).*(error|failed|handshake|certificate)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: false),

        new ErrorPattern(
            "Network",
            new Regex(@"(TaskCanceledException|request timed out|operation timed out|timeout.*exceeded)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Medium",
            IsTransient: true),

        // File system errors
        new ErrorPattern(
            "FileSystem",
            new Regex(@"(IOException|disk.*(full|space)|not enough (disk )?space|There is not enough space|no space left)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Critical",
            IsTransient: false),

        new ErrorPattern(
            "FileSystem",
            new Regex(@"(file.*(not found|does not exist|missing)|FileNotFoundException|DirectoryNotFoundException|Could not find (file|path|a part of the path))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: false),

        new ErrorPattern(
            "FileSystem",
            new Regex(@"(file.*(in use|locked|being used)|sharing violation|cannot access.*because.*another process|The process cannot access the file)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: true),

        // Permission errors
        new ErrorPattern(
            "Permission",
            new Regex(@"(access.*(denied|is denied)|UnauthorizedAccessException|permission denied|insufficient privileges|not authorized|elevation required|requires administrator)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: false),

        // Service errors
        new ErrorPattern(
            "Service",
            new Regex(@"(service.*(failed to start|could not be started|did not respond|timed out|not found|already running|stopped unexpectedly)|sc\.exe.*failed|StartService.*failed|ControlService.*failed)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: true),

        new ErrorPattern(
            "Service",
            new Regex(@"(service.*(not installed|does not exist)|The specified service does not exist)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Critical",
            IsTransient: false),

        // Package/checksum errors
        new ErrorPattern(
            "Package",
            new Regex(@"(checksum.*(mismatch|failed|invalid|does not match)|hash.*(mismatch|verification failed)|corrupt(ed)?.*(file|package|archive|download)|invalid (zip|package|archive)|bad (zip|archive) file|End of Central Directory)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: true),

        new ErrorPattern(
            "Package",
            new Regex(@"(extract(ion)?.*(failed|error)|unzip.*failed|ZipException|InvalidDataException)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: true),

        // Configuration errors
        new ErrorPattern(
            "Configuration",
            new Regex(@"(appsettings.*not found|configuration.*(missing|invalid|error)|missing required.*setting|invalid.*(url|endpoint|api.?key)|connection string.*(missing|invalid))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: false),

        // Resource/memory errors
        new ErrorPattern(
            "Resource",
            new Regex(@"(OutOfMemoryException|StackOverflowException|insufficient memory|memory allocation failed|System\.OutOfMemory)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Critical",
            IsTransient: true),

        // Generic exception patterns (lower priority)
        new ErrorPattern(
            "Unknown",
            new Regex(@"(?:^|\n).*(?:FATAL|CRITICAL|Unhandled exception).*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Critical",
            IsTransient: false),

        new ErrorPattern(
            "Unknown",
            new Regex(@"(?:^|\n).*(?:\bERROR\b).*Exception.*", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "High",
            IsTransient: false),
    };

    // Phase detection patterns — map log content to update phases
    private static readonly (Regex Pattern, string Phase)[] PhasePatterns = new[]
    {
        (new Regex(@"download|Downloading", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Downloading"),
        (new Regex(@"verif|checksum|hash", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Verifying"),
        (new Regex(@"extract|unzip|decompress", RegexOptions.IgnoreCase | RegexOptions.Compiled), "Extracting"),
        (new Regex(@"updater|preparing.?updater", RegexOptions.IgnoreCase | RegexOptions.Compiled), "PreparingUpdater"),
        (new Regex(@"stop.*service|sc\.exe stop|StoppingService", RegexOptions.IgnoreCase | RegexOptions.Compiled), "StoppingService"),
        (new Regex(@"backup|backing.?up", RegexOptions.IgnoreCase | RegexOptions.Compiled), "BackingUp"),
        (new Regex(@"copy|copying.*files|xcopy|robocopy", RegexOptions.IgnoreCase | RegexOptions.Compiled), "CopyingFiles"),
        (new Regex(@"start.*service|sc\.exe start|StartingService", RegexOptions.IgnoreCase | RegexOptions.Compiled), "StartingService"),
    };

    public ErrorAnalyzerService(ILogger<ErrorAnalyzerService> logger)
    {
        _logger = logger;
    }

    public ErrorAnalysisResult Analyze(int computerId, string rawErrorLogs, AgentUpdateStatus lastStatus)
    {
        var result = new ErrorAnalysisResult
        {
            ComputerId = computerId,
            AnalyzedAt = DateTime.UtcNow
        };

        // Detect the phase where failure occurred
        result.FailedPhase = DetectFailedPhase(rawErrorLogs, lastStatus);

        // Extract and categorize errors
        result.Errors = ExtractErrors(rawErrorLogs);

        // Determine overall severity
        result.Severity = DetermineOverallSeverity(result.Errors);

        // Check if the issue is transient
        result.IsTransient = result.Errors.Count > 0 && result.Errors.All(e =>
            IsTransientCategory(e.Category, rawErrorLogs));

        // Generate summary
        result.Summary = GenerateSummary(result);

        // Generate remediation steps
        result.RemediationSteps = GenerateRemediationSteps(result);

        // Determine suggested action
        result.SuggestedAction = DetermineSuggestedAction(result);

        _logger.LogInformation(
            "Error analysis for computer {ComputerId}: {ErrorCount} errors found, severity={Severity}, phase={Phase}, transient={IsTransient}",
            computerId, result.Errors.Count, result.Severity, result.FailedPhase, result.IsTransient);

        return result;
    }

    private string DetectFailedPhase(string logs, AgentUpdateStatus lastStatus)
    {
        // If the status directly indicates a phase, use it
        if (lastStatus is > AgentUpdateStatus.Pending and < AgentUpdateStatus.Completed)
        {
            return lastStatus.ToString();
        }

        // Scan the logs from bottom up (last phase mentioned is likely where it failed)
        string detectedPhase = "Unknown";
        int latestPosition = -1;

        foreach (var (pattern, phase) in PhasePatterns)
        {
            var matches = pattern.Matches(logs);
            if (matches.Count > 0)
            {
                var lastMatch = matches[^1];
                if (lastMatch.Index > latestPosition)
                {
                    latestPosition = lastMatch.Index;
                    detectedPhase = phase;
                }
            }
        }

        return detectedPhase;
    }

    private List<IdentifiedError> ExtractErrors(string rawLogs)
    {
        var errors = new List<IdentifiedError>();
        var seenMessages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Split into log sections for source attribution
        var currentSource = "general";
        var lines = rawLogs.Split('\n');

        foreach (var line in lines)
        {
            // Track which log section we're in
            if (line.StartsWith("===") && line.EndsWith("==="))
            {
                currentSource = line.Trim('=', ' ');
                continue;
            }

            foreach (var pattern in ErrorPatterns)
            {
                var match = pattern.Regex.Match(line);
                if (match.Success)
                {
                    // Extract a clean error message (the matched line, trimmed)
                    var errorMessage = line.Trim();
                    if (errorMessage.Length > 500)
                        errorMessage = errorMessage[..500] + "...";

                    // Deduplicate
                    var dedupeKey = $"{pattern.Category}:{match.Value.Trim()}";
                    if (seenMessages.Add(dedupeKey))
                    {
                        errors.Add(new IdentifiedError
                        {
                            Category = pattern.Category,
                            Message = errorMessage,
                            Source = currentSource,
                            Severity = pattern.Severity
                        });
                    }
                }
            }
        }

        // If no specific errors found, add a generic one
        if (errors.Count == 0)
        {
            // Try to find any line with "error" or "fail" keywords
            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"\b(error|fail(ed)?|exception)\b", RegexOptions.IgnoreCase)
                    && !line.StartsWith("===")
                    && line.Trim().Length > 10)
                {
                    var msg = line.Trim();
                    if (msg.Length > 500) msg = msg[..500] + "...";
                    errors.Add(new IdentifiedError
                    {
                        Category = "Unknown",
                        Message = msg,
                        Source = currentSource,
                        Severity = "Medium"
                    });
                    if (errors.Count >= 5) break; // Cap generic matches
                }
            }
        }

        return errors;
    }

    private bool IsTransientCategory(string category, string logs)
    {
        // Look up the first matching pattern for this category to check transient flag
        foreach (var pattern in ErrorPatterns)
        {
            if (pattern.Category == category && pattern.Regex.IsMatch(logs))
            {
                return pattern.IsTransient;
            }
        }
        return false;
    }

    private static string DetermineOverallSeverity(List<IdentifiedError> errors)
    {
        if (errors.Count == 0) return "Medium";

        var severityOrder = new Dictionary<string, int>
        {
            ["Low"] = 0, ["Medium"] = 1, ["High"] = 2, ["Critical"] = 3
        };

        var maxSeverity = errors.Max(e => severityOrder.GetValueOrDefault(e.Severity, 1));
        return severityOrder.First(kv => kv.Value == maxSeverity).Key;
    }

    private static string GenerateSummary(ErrorAnalysisResult result)
    {
        if (result.Errors.Count == 0)
            return $"Update failed during {result.FailedPhase} phase. No specific error pattern identified in the logs.";

        var primaryError = result.Errors[0];
        var categoryDescriptions = new Dictionary<string, string>
        {
            ["Network"] = "network connectivity issue",
            ["FileSystem"] = "file system error",
            ["Permission"] = "permission/access issue",
            ["Service"] = "Windows service management error",
            ["Package"] = "update package integrity issue",
            ["Configuration"] = "configuration error",
            ["Resource"] = "system resource exhaustion",
            ["Unknown"] = "unexpected error"
        };

        var desc = categoryDescriptions.GetValueOrDefault(primaryError.Category, "unexpected error");
        var transientNote = result.IsTransient ? " This appears to be a transient issue that may resolve on retry." : "";

        return $"Update failed during {result.FailedPhase} phase due to a {desc}.{transientNote}";
    }

    private List<RemediationStep> GenerateRemediationSteps(ErrorAnalysisResult result)
    {
        var steps = new List<RemediationStep>();
        var addedCategories = new HashSet<string>();
        int order = 1;

        foreach (var error in result.Errors)
        {
            if (!addedCategories.Add(error.Category))
                continue; // Skip duplicate categories

            switch (error.Category)
            {
                case "Network":
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Verify network connectivity",
                        Description = "Ensure the computer has a stable network connection to the update server. Check firewall rules, proxy settings, and DNS resolution. Verify the API server URL is reachable from the target machine."
                    });
                    if (result.IsTransient)
                    {
                        steps.Add(new RemediationStep
                        {
                            Order = order++,
                            Title = "Retry the update",
                            Description = "This appears to be a transient network issue. Retry the update — the agent will re-download the package on the next attempt.",
                            IsAutomatable = true,
                            AutomationAction = "force-update"
                        });
                    }
                    break;

                case "FileSystem" when error.Message.Contains("space", StringComparison.OrdinalIgnoreCase)
                                    || error.Message.Contains("disk", StringComparison.OrdinalIgnoreCase):
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Free disk space",
                        Description = "The target machine does not have enough disk space for the update. Free at least 200 MB on the system drive. Check C:\\ProgramData\\ComputerInventory and temp folders for stale files."
                    });
                    break;

                case "FileSystem" when error.Message.Contains("in use", StringComparison.OrdinalIgnoreCase)
                                    || error.Message.Contains("locked", StringComparison.OrdinalIgnoreCase)
                                    || error.Message.Contains("another process", StringComparison.OrdinalIgnoreCase):
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Close conflicting processes",
                        Description = "A file required by the update is locked by another process. Restart the computer to release file locks, then retry the update.",
                        IsAutomatable = false
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Retry the update after reboot",
                        Description = "After the computer restarts, trigger the update again.",
                        IsAutomatable = true,
                        AutomationAction = "force-update"
                    });
                    break;

                case "FileSystem":
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Check file system integrity",
                        Description = "Required files or directories are missing. Verify the agent installation directory exists at the expected path. A full reinstall may be needed to restore the directory structure.",
                        IsAutomatable = true,
                        AutomationAction = "force-reinstall"
                    });
                    break;

                case "Permission":
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Verify service account permissions",
                        Description = "The agent service does not have sufficient permissions. Ensure the ComputerInventory service runs as Local System or an account with write access to the installation directory and C:\\ProgramData\\ComputerInventory."
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Reinstall the agent",
                        Description = "If permissions cannot be fixed manually, reinstall the agent to reset service configuration and folder ACLs.",
                        IsAutomatable = true,
                        AutomationAction = "force-reinstall"
                    });
                    break;

                case "Service" when error.Message.Contains("not installed", StringComparison.OrdinalIgnoreCase)
                                  || error.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase):
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Reinstall the agent service",
                        Description = "The Windows service is not registered. The agent must be reinstalled to re-register the service.",
                        IsAutomatable = true,
                        AutomationAction = "force-reinstall"
                    });
                    break;

                case "Service":
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Check Windows service state",
                        Description = "The agent service failed to stop or start during the update. Open Services (services.msc) on the target machine and check the ComputerInventory service status. If it's stuck, restart the computer."
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Retry the update",
                        Description = "After verifying the service state, retry the update.",
                        IsAutomatable = true,
                        AutomationAction = "force-update"
                    });
                    break;

                case "Package":
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Re-download update package",
                        Description = "The update package was corrupted during download or on disk. Retry the update — the agent will download a fresh copy and re-verify the checksum.",
                        IsAutomatable = true,
                        AutomationAction = "force-update"
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Verify server-side package",
                        Description = "If retry fails again, verify the update package on the server. Re-upload or rebuild the package if the checksum is incorrect."
                    });
                    break;

                case "Configuration":
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Check agent configuration",
                        Description = "The agent configuration (appsettings.json) may be missing or contain invalid settings. Verify the API URL, API key, and other required settings on the target machine at the agent installation directory."
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Reinstall with correct configuration",
                        Description = "If configuration cannot be fixed in place, reinstall the agent to deploy a fresh configuration.",
                        IsAutomatable = true,
                        AutomationAction = "force-reinstall"
                    });
                    break;

                case "Resource":
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Check system resources",
                        Description = "The target machine ran out of memory or system resources during the update. Close unnecessary applications and ensure the machine has adequate RAM. Check for memory leaks in other running software."
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Retry after freeing resources",
                        Description = "After ensuring sufficient resources, retry the update.",
                        IsAutomatable = true,
                        AutomationAction = "force-update"
                    });
                    break;

                default: // Unknown
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Review error logs",
                        Description = "Download and review the full error logs for details. The error pattern was not automatically recognized. Look for exception stack traces and error messages near the end of the log."
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Retry the update",
                        Description = "If the error appears transient, retry the update.",
                        IsAutomatable = true,
                        AutomationAction = "force-update"
                    });
                    steps.Add(new RemediationStep
                    {
                        Order = order++,
                        Title = "Reinstall as last resort",
                        Description = "If retrying fails, perform a full agent reinstall.",
                        IsAutomatable = true,
                        AutomationAction = "force-reinstall"
                    });
                    break;
            }
        }

        // If no steps generated (no errors found), add generic steps
        if (steps.Count == 0)
        {
            steps.Add(new RemediationStep
            {
                Order = 1,
                Title = "Review error logs",
                Description = "Download the full error logs and review them manually. No specific error pattern was identified."
            });
            steps.Add(new RemediationStep
            {
                Order = 2,
                Title = "Retry the update",
                Description = "Try running the update again — the issue may have been transient.",
                IsAutomatable = true,
                AutomationAction = "force-update"
            });
            steps.Add(new RemediationStep
            {
                Order = 3,
                Title = "Reinstall the agent",
                Description = "If the update continues to fail, perform a clean reinstall.",
                IsAutomatable = true,
                AutomationAction = "force-reinstall"
            });
        }

        return steps;
    }

    private static string DetermineSuggestedAction(ErrorAnalysisResult result)
    {
        if (result.IsTransient)
            return "Retry";

        // If any error is a service-not-installed or permission or missing files, suggest reinstall
        var needsReinstall = result.Errors.Any(e =>
            e.Category == "Permission" ||
            (e.Category == "Service" && (e.Message.Contains("not installed", StringComparison.OrdinalIgnoreCase)
                                       || e.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase))) ||
            (e.Category == "FileSystem" && e.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)) ||
            e.Category == "Configuration");

        if (needsReinstall)
            return "Reinstall";

        // If critical resource issue, manual intervention needed
        if (result.Severity == "Critical")
            return "ManualIntervention";

        return "Retry";
    }

    private sealed record ErrorPattern(string Category, Regex Regex, string Severity, bool IsTransient);
}
