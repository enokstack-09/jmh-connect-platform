namespace JMHConnect.Api.Options;

public sealed class EmailSetupOptions
{
    public const string SectionName = "EmailSetup";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? FromAddress { get; set; }
    public string FromName { get; set; } = "JMH Connect";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = true;
}
