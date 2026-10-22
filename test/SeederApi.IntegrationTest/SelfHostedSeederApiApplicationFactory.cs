namespace Bit.SeederApi.IntegrationTest;

public class SelfHostedSeederApiApplicationFactory : InPlaySeederApiApplicationFactory
{
    public SelfHostedSeederApiApplicationFactory()
    {
        UpdateConfiguration("globalSettings:selfHosted", "true");
        // AddPush rejects self-hosted startup without an installation id.
        UpdateConfiguration("globalSettings:installation:id", "10000000-0000-0000-0000-000000000000");
        // Self-hosted defaults data protection keys to /etc/bitwarden, which is not writable on CI.
        UpdateConfiguration("globalSettings:dataProtection:directory",
            Path.Combine(Path.GetTempPath(), $"seeder-dp-{Guid.NewGuid()}"));
        UpdateConfiguration("globalSettings:licenseCertificatePath", null);
        UpdateConfiguration("globalSettings:licenseCertificatePassword", null);
    }
}
