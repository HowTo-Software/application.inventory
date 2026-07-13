namespace ComputerInventory.Contracts;

/// <summary>
/// Centralized API endpoint constants to eliminate magic strings throughout services.
/// </summary>
public static class ApiEndpoints
{
    public static class Auth
    {
        public const string Login = "api/auth/login";
        public const string Logout = "api/auth/logout";
        public const string Validate = "api/auth/validate";
        public const string Refresh = "api/auth/refresh";
        public const string Me = "api/auth/me";
        public const string ChangePassword = "api/auth/change-password";
        public const string UpdateProfile = "api/auth/profile";
        public const string InviteCodes = "api/auth/invite-codes";
        public const string VerifyEmail = "api/auth/verify-email";
        public const string ResendVerification = "api/auth/resend-verification";

        public static string RevokeInviteCode(int id) => $"api/auth/invite-codes/{id}";
    }

    public static class Computers
    {
        public const string Base = "api/computers";
        public const string Paged = "api/computers/paged";
        public const string Stats = "api/computers/stats";
        public const string Export = "api/computers/export";
        public const string Import = "api/computers/import";
        public const string BulkStatus = "api/computers/bulk-status";
        public const string BulkDelete = "api/computers/bulk-delete";

        public static string ById(int id) => $"api/computers/{id}";
        public static string Search(string term) => $"api/computers/search?term={Uri.EscapeDataString(term)}";
        public static string Status(int id) => $"api/computers/{id}/status";
        public static string Assign(int id) => $"api/computers/{id}/assign";
        public static string ForceUpdate(int id) => $"api/computers/{id}/force-update";
        public static string CancelUpdate(int id) => $"api/computers/{id}/cancel-update";
        public static string RequestCheckin(int id) => $"api/computers/{id}/request-checkin";
        public static string ErrorLogs(int id) => $"api/computers/{id}/error-logs";
        public static string ErrorAnalysis(int id) => $"api/computers/{id}/error-analysis";
        public static string OverrideUpdate(int id) => $"api/computers/{id}/override-update";
    }

    public static class Maintenance
    {
        public const string Base = "api/maintenance";
        public const string Paged = "api/maintenance/paged";

        public static string ById(int id) => $"api/maintenance/{id}";
        public static string ByComputerId(int computerId) => $"api/maintenance/computer/{computerId}";
        public static string Complete(int id) => $"api/maintenance/{id}/complete";
    }

    public static class Users
    {
        public const string Base = "api/users";

        public static string ById(int id) => $"api/users/{id}";
        public static string Activate(int id) => $"api/users/{id}/activate";
        public static string Deactivate(int id) => $"api/users/{id}/deactivate";
    }

    public static class Lookups
    {
        public const string Models = "api/lookups/models";

        public static string ByEndpoint(string endpoint) => $"api/lookups/{endpoint}";
        public static string ModelsByManufacturer(int manufacturerId) => $"api/lookups/models?manufacturerId={manufacturerId}";
    }

    public static class SystemDetails
    {
        public const string Report = "api/systemdetails/report";
        public const string ReportSummaries = "api/systemdetails/report/summaries";
        public const string Submit = "api/systemdetails/submit";

        public static string ByComputerId(int computerId) => $"api/systemdetails/computer/{computerId}";
        public static string History(int computerId) => $"api/systemdetails/computer/{computerId}/history";
    }

    public static class Map
    {
        public const string Locations = "api/map/locations";
        public const string LastRefresh = "api/map/last-refresh";
        public const string Refresh = "api/map/refresh";

        public static string RefreshComputer(int computerId) => $"api/map/refresh/{computerId}";
        public static string LocationSource(int computerId) => $"api/map/location-source/{computerId}";
    }

    public static class Departments
    {
        public const string Base = "api/departments";

        public static string ById(int id) => $"api/departments/{id}";
    }

    public static class Locations
    {
        public const string Base = "api/locations";

        public static string ById(int id) => $"api/locations/{id}";
    }

    public static class Packages
    {
        public const string Base = "api/packages";
        public const string Upload = "api/packages/upload";
        public const string DistributionSummary = "api/packages/distribution-summary";

        public static string ById(int id) => $"api/packages/{id}";
        public static string Publish(int id) => $"api/packages/{id}/publish";
        public static string Activate(int id) => $"api/packages/{id}/activate";
        public static string Deprecate(int id) => $"api/packages/{id}/deprecate";
        public static string Rollback(int id) => $"api/packages/{id}/rollback";
        public static string Rules(int packageId) => $"api/packages/{packageId}/rules";
        public static string RemoveRule(int packageId, int ruleId) => $"api/packages/{packageId}/rules/{ruleId}";
    }

    public static class Dashboard
    {
        public const string Base = "api/dashboard";
    }

    public static class AuditLog
    {
        public const string Base = "api/auditlog";
        public const string Purge = "api/auditlog/purge";
    }

    public static class Version
    {
        public const string History = "api/version/history";
        public const string ServerCurrent = "api/version/server/current";
        public const string ThinClientCurrent = "api/version/thinclient/current";

        public static string HistoryById(int id) => $"api/version/history/{id}";
    }

    public static class Agent
    {
        public const string Register = "api/agent/register";
        public const string Heartbeat = "api/agent/heartbeat";
        public const string Checkin = "api/agent/checkin";
        public const string Checkout = "api/agent/checkout";
        public const string UpdateProgress = "api/agent/update-progress";
    }

    public static class RestorePoint
    {
        public const string Base = "api/restorepoint";
    }

    public static class Health
    {
        public const string Base = "api/health";
    }
}
