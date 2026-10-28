using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates one <see cref="Policy"/> row per entry for the organization, enabled or disabled.
/// </summary>
/// <remarks>
/// CreationDate and RevisionDate keep the entity default (seed time) because their setters are internal to Core.
/// The step writes the rows only; it does not run the server's policy side effects.
/// </remarks>
internal sealed class CreatePoliciesStep(IReadOnlyList<(PolicyType Type, bool Enabled, string? Data)> policies) : IStep
{
    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();

        foreach (var (type, enabled, data) in policies)
        {
            context.Policies.Add(new Policy
            {
                Id = CombGuid.Generate(),
                OrganizationId = orgId,
                Type = type,
                Enabled = enabled,
                Data = data ?? DefaultData(type),
            });
        }
    }

    // Clients only run the personal-item transfer when enableIndividualItemsTransfer is true.
    private static string? DefaultData(PolicyType type) => type switch
    {
        PolicyType.OrganizationDataOwnership => """{"enableIndividualItemsTransfer":true}""",
        _ => null,
    };
}
