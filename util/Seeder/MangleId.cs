namespace Bit.Seeder;

public class MangleId
{
    public readonly string Value;

    public MangleId()
    {
        // Generate a short random string (6 char) to use as the mangle ID
        Value = Random.Shared.NextInt64().ToString("x").Substring(0, 8);
    }

    public override string ToString() => Value;
}
