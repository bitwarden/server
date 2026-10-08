using Bitwarden.Server.Sdk.Features;

namespace Bit.AgentFill;

/// <summary>
/// Feature flag keys owned by <c>Bit.AgentFill</c>. <see cref="AgentFillServiceCollectionExtensions.AddAgentFill"/>
/// registers these as known flags.
/// </summary>
[FlagKeyCollection]
public static partial class AgentFillFeatureFlags
{
    /// <summary>Gates the agent fill approval endpoints and pushes.</summary>
    public const string AgentFillApprovals = "ai-136-agent-fill-approvals";
}
