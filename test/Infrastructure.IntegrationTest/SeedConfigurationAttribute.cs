namespace Bit.Infrastructure.IntegrationTest;

[AttributeUsage(AttributeTargets.Assembly)]
public class SeedConfigurationAttribute<T> : Attribute
    where T : ISeeder
{
    public SeedConfigurationAttribute(string seedName)
    {
        SeedName = seedName;
    }

    public string SeedName { get; }
}
