namespace Bit.SeederApi.Utilities;

public class BasicAuth
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class SeederSettings
{
    public BasicAuth[] Accounts { get; set; } = [];
}
