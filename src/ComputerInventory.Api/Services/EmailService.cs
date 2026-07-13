using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Polly;
using Polly.Retry;

namespace ComputerInventory.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    private static readonly AsyncRetryPolicy _smtpRetryPolicy = Policy
        .Handle<SmtpCommandException>(ex => (int)ex.StatusCode >= 400 && (int)ex.StatusCode < 500 == false)
        .Or<SmtpProtocolException>()
        .Or<System.IO.IOException>()
        .Or<System.Net.Sockets.SocketException>()
        .WaitAndRetryAsync(3,
            attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

    public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuration["EmailSettings:SmtpHost"]);

    public async Task SendVerificationEmailAsync(string toEmail, string username, string verificationToken)
    {
        var baseUrl = _configuration["AgentSettings:ServerUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("ServerUrl not configured — cannot send verification email to {Email}", toEmail);
            return;
        }

        var verifyUrl = $"{baseUrl}/verify-email?token={Uri.EscapeDataString(verificationToken)}";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            _configuration["EmailSettings:FromName"] ?? "Computer Inventory",
            _configuration["EmailSettings:FromAddress"] ?? "noreply@example.com"));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = "Verify your email address";

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = $"""
                <div style="font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;">
                    <h2 style="color: #333;">Verify your email address</h2>
                    <p>Hi {System.Net.WebUtility.HtmlEncode(username)},</p>
                    <p>Thank you for registering. Please verify your email address by clicking the button below:</p>
                    <p style="text-align: center; margin: 30px 0;">
                        <a href="{System.Net.WebUtility.HtmlEncode(verifyUrl)}"
                           style="background-color: #0d6efd; color: #fff; padding: 12px 24px; text-decoration: none; border-radius: 6px; display: inline-block;">
                            Verify Email
                        </a>
                    </p>
                    <p style="color: #666; font-size: 14px;">
                        If the button doesn't work, copy and paste this link into your browser:<br/>
                        <a href="{System.Net.WebUtility.HtmlEncode(verifyUrl)}">{System.Net.WebUtility.HtmlEncode(verifyUrl)}</a>
                    </p>
                    <p style="color: #999; font-size: 12px;">This link expires in 24 hours. If you didn't create an account, you can ignore this email.</p>
                </div>
                """,
            TextBody = $"Hi {username},\n\nVerify your email address by visiting:\n{verifyUrl}\n\nThis link expires in 24 hours."
        };

        message.Body = bodyBuilder.ToMessageBody();

        var host = _configuration["EmailSettings:SmtpHost"];
        var port = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
        var smtpUser = _configuration["EmailSettings:SmtpUser"];
        var smtpPass = _configuration["EmailSettings:SmtpPass"];

        await _smtpRetryPolicy.ExecuteAsync(async () =>
        {
            using var client = new SmtpClient();
            try
            {
                await client.ConnectAsync(host, port, SecureSocketOptions.StartTls);

                if (!string.IsNullOrWhiteSpace(smtpUser))
                {
                    await client.AuthenticateAsync(smtpUser, smtpPass);
                }

                await client.SendAsync(message);
                _logger.LogInformation("Verification email sent to {Email}", toEmail);
            }
            finally
            {
                await client.DisconnectAsync(true);
            }
        });
    }
}
