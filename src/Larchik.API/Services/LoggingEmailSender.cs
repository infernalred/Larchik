using System.Text;

namespace Larchik.API.Services;

public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== Email ===");
        builder.AppendLine($"To: {email}");
        builder.AppendLine($"Subject: {subject}");
        builder.AppendLine("Body:");
        builder.AppendLine(htmlMessage);
        builder.AppendLine("=============");
        logger.LogInformation("{EmailBody}", builder.ToString());
        return Task.CompletedTask;
    }
}
