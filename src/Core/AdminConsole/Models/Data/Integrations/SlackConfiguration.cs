namespace Bit.Core.Models.Data.Integrations;

public class SlackConfiguration
{
    public string Token { get; set; } = string.Empty;
    public List<string> Channels { get; set; } = new();
    public List<string> UserEmails { get; set; } = new();
}
