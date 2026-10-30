using System.Net;
using System.Net.Mail;
using JMHConnect.Api.Options;
using Microsoft.Extensions.Options;

namespace JMHConnect.Api.Services;

public sealed class SmtpAccountSetupEmailSender(IOptions<EmailSetupOptions> options)
    : IAccountSetupEmailSender
{
    public async Task SendSetupLinkAsync(
        string email,
        Uri setupLink,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.SmtpHost) ||
            string.IsNullOrWhiteSpace(settings.FromAddress))
        {
            throw new InvalidOperationException("Account setup email is not configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromAddress, settings.FromName),
            Subject = "Set up your JMH Connect account",
            Body = $"Use this one-time link to set your password: {setupLink}\n\nIf you did not expect this message, you can ignore it.",
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.UseTls,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
