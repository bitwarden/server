using Bit.Pam.Enums;

namespace Bit.Commercial.Pam.Api.Models.Response;

/// <summary>
/// Maps the backend <see cref="AccessDeciderKind"/> to the vocabulary the client expects on a decision:
/// <c>human | automatic</c>. Mirrors <see cref="AccessRequestStatusNames"/> / <see cref="AccessLeaseStatusNames"/>.
/// </summary>
public static class AccessDeciderKindNames
{
    public const string Human = "human";
    public const string Automatic = "automatic";

    public static string From(AccessDeciderKind kind) => kind switch
    {
        AccessDeciderKind.Human => Human,
        AccessDeciderKind.Automatic => Automatic,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
