using Bit.Seeder.Data.Enums;

namespace Bit.Seeder.Data.Distributions;

/// <summary>
/// Pre-configured permission type distributions organized by org size and pendulum position.
/// Every distribution guarantees at least 5% Manage and 5% ReadWrite.
/// </summary>
public static class PermissionDistributions
{
    /// <summary>
    /// Enterprise, read-heavy. Our production baseline. Pendulum swings hard toward ReadOnly.
    /// </summary>
    public static Distribution<PermissionWeight> Enterprise { get; } = new(
        (PermissionWeight.ReadOnly, 0.82),
        (PermissionWeight.ReadWrite, 0.09),
        (PermissionWeight.Manage, 0.05),
        (PermissionWeight.HidePasswords, 0.04)
    );

    /// <summary>
    /// Enterprise, write-heavy. Engineering-driven orgs where most users need to edit shared credentials.
    /// </summary>
    public static Distribution<PermissionWeight> EnterpriseWriteHeavy { get; } = new(
        (PermissionWeight.ReadWrite, 0.55),
        (PermissionWeight.ReadOnly, 0.25),
        (PermissionWeight.Manage, 0.10),
        (PermissionWeight.HidePasswords, 0.10)
    );

    /// <summary>
    /// Enterprise, manage-heavy. Decentralized admin model with many collection managers.
    /// </summary>
    public static Distribution<PermissionWeight> EnterpriseManageHeavy { get; } = new(
        (PermissionWeight.Manage, 0.30),
        (PermissionWeight.ReadWrite, 0.30),
        (PermissionWeight.ReadOnly, 0.30),
        (PermissionWeight.HidePasswords, 0.10)
    );

    /// <summary>
    /// Mid-market, read-heavy. Structured org where most users consume, leads manage.
    /// </summary>
    public static Distribution<PermissionWeight> MidMarket { get; } = new(
        (PermissionWeight.ReadOnly, 0.55),
        (PermissionWeight.ReadWrite, 0.20),
        (PermissionWeight.Manage, 0.15),
        (PermissionWeight.HidePasswords, 0.10)
    );

    /// <summary>
    /// Mid-market, write-heavy. Collaborative teams where most users create and edit.
    /// </summary>
    public static Distribution<PermissionWeight> MidMarketWriteHeavy { get; } = new(
        (PermissionWeight.ReadWrite, 0.50),
        (PermissionWeight.Manage, 0.20),
        (PermissionWeight.ReadOnly, 0.20),
        (PermissionWeight.HidePasswords, 0.10)
    );

    /// <summary>
    /// Mid-market, manage-heavy. Flat org where many people own their collections.
    /// </summary>
    public static Distribution<PermissionWeight> MidMarketManageHeavy { get; } = new(
        (PermissionWeight.Manage, 0.40),
        (PermissionWeight.ReadWrite, 0.30),
        (PermissionWeight.ReadOnly, 0.20),
        (PermissionWeight.HidePasswords, 0.10)
    );

    /// <summary>
    /// Small business, read-heavy. Tighter controls despite small size — onboarding, contractors.
    /// </summary>
    public static Distribution<PermissionWeight> SmallBusiness { get; } = new(
        (PermissionWeight.ReadOnly, 0.40),
        (PermissionWeight.ReadWrite, 0.30),
        (PermissionWeight.Manage, 0.25),
        (PermissionWeight.HidePasswords, 0.05)
    );

    /// <summary>
    /// Small business, write-heavy. High-trust team where most people edit freely.
    /// </summary>
    public static Distribution<PermissionWeight> SmallBusinessWriteHeavy { get; } = new(
        (PermissionWeight.ReadWrite, 0.45),
        (PermissionWeight.Manage, 0.35),
        (PermissionWeight.ReadOnly, 0.15),
        (PermissionWeight.HidePasswords, 0.05)
    );

    /// <summary>
    /// Small business, manage-heavy. Founders and senior staff own most collections.
    /// </summary>
    public static Distribution<PermissionWeight> SmallBusinessManageHeavy { get; } = new(
        (PermissionWeight.Manage, 0.50),
        (PermissionWeight.ReadWrite, 0.30),
        (PermissionWeight.ReadOnly, 0.15),
        (PermissionWeight.HidePasswords, 0.05)
    );

    /// <summary>
    /// Teams Starter. Tiny high-trust team — heavy Manage, everyone contributes.
    /// </summary>
    public static Distribution<PermissionWeight> TeamsStarter { get; } = new(
        (PermissionWeight.Manage, 0.50),
        (PermissionWeight.ReadWrite, 0.40),
        (PermissionWeight.ReadOnly, 0.10)
    );

    /// <summary>
    /// Families plan. Shared household — nearly everyone manages everything.
    /// </summary>
    public static Distribution<PermissionWeight> Family { get; } = new(
        (PermissionWeight.Manage, 0.70),
        (PermissionWeight.ReadWrite, 0.20),
        (PermissionWeight.ReadOnly, 0.10)
    );
}
