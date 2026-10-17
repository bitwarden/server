namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Guards for access-shape loops that stop after a bounded number of attempts. A seeded org that quietly falls short
/// of its preset invalidates the performance test it was built for, so a real shortfall fails the run.
/// </summary>
internal static class ShapeTargets
{
    /// <summary>Allowed relative miss before a target counts as not reached.</summary>
    internal const double Tolerance = 0.01;

    internal static void EnsureReached(string what, long actual, long target)
    {
        if (actual < target * (1 - Tolerance))
        {
            throw new InvalidOperationException(
                $"Access shape produced {actual:N0} {what} but the preset asks for {target:N0} (more than {Tolerance:P0} short). " +
                "The caps or histogram leave no room to reach it; loosen them or lower the target.");
        }
    }
}
