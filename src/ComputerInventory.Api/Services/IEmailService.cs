namespace ComputerInventory.Api.Services;

public interface IEmailService
{
    bool IsConfigured { get; }
    Task SendVerificationEmailAsync(string toEmail, string username, string verificationToken);
}
