namespace JMHConnect.Api.Services;

public interface IAccountSetupEmailSender
{
    Task SendSetupLinkAsync(string email, Uri setupLink, CancellationToken cancellationToken);
}
