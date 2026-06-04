using Bit.Core.Pam.Engine;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Test.Pam.Engine;

public sealed class FakeAccessRuleResolver : IAccessRuleResolver
{
    private readonly Dictionary<Guid, AccessRule> _rules = [];

    public void SetRule(Guid cipherId, AccessRule rule) => _rules[cipherId] = rule;

    public AccessRule? Resolve(CipherDetails cipher)
        => _rules.TryGetValue(cipher.Id, out var rule) ? rule : null;
}
