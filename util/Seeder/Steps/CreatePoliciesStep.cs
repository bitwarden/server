using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Utilities;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates <see cref="Policy"/> rows (enabled or disabled) for the organization.
/// </summary>
/// <remarks>
/// CreationDate/RevisionDate keep their entity default (now): their setters are internal to Core.
/// Only writes the row; it does not run the server's policy side effects. Seed those explicitly
/// (e.g. <see cref="CreateMyItemsStep"/> for <see cref="PolicyType.OrganizationDataOwnership"/>).
/// </remarks>
internal sealed class CreatePoliciesStep(IReadOnlyList<(PolicyType Type, bool Enabled, string? Data)> policies) : IStep
{
    public void Execute(SeederContext context)
    {
        var orgId = context.RequireOrgId();

        foreach (var (type, enabled, data) in policies.DistinctBy(p => p.Type))
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

    // Clients only run the My Items migration when this flag is set (libs/vault default-vault-items-transfer.service.ts)
    private static string? DefaultData(PolicyType type) => type switch
    {
        PolicyType.OrganizationDataOwnership => """{"enableIndividualItemsTransfer":true}""",
        _ => null,
    };
}
