namespace Larchik.API.Services;

public interface IEmailSender
{
    Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken = default);
}
