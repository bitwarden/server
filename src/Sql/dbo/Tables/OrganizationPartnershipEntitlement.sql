CREATE TABLE [dbo].[OrganizationPartnershipEntitlement]
(
    [Id]                         UNIQUEIDENTIFIER NOT NULL,
    [OrganizationPartnershipId]  UNIQUEIDENTIFIER NOT NULL,
    [ExternalId]                 NVARCHAR(MAX)    NOT NULL,
    [ExternalIdHash]             VARCHAR(64)      NOT NULL,
    [State]                      TINYINT          NOT NULL,
    [UserId]                     UNIQUEIDENTIFIER NULL,
    [AccountRef]                 UNIQUEIDENTIFIER NULL,
    [Metadata]                   NVARCHAR(MAX)    NULL,
    [BoundDate]                  DATETIME2(7)     NULL,
    [SuspendedDate]              DATETIME2(7)     NULL,
    [CanceledDate]               DATETIME2(7)     NULL,
    [ResumeWindowExpirationDate] DATETIME2(7)     NULL,
    [LastAppliedEffectiveDate]   DATETIME2(7)     NOT NULL,
    [CreationDate]               DATETIME2(7)     NOT NULL,
    [RevisionDate]               DATETIME2(7)     NOT NULL,
    CONSTRAINT [PK_OrganizationPartnershipEntitlement] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationPartnershipEntitlement_OrganizationPartnership] FOREIGN KEY ([OrganizationPartnershipId]) REFERENCES [dbo].[OrganizationPartnership] ([Id]),
    CONSTRAINT [FK_OrganizationPartnershipEntitlement_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);
GO

CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_OrganizationPartnershipId_ExternalIdHash]
    ON [dbo].[OrganizationPartnershipEntitlement]([OrganizationPartnershipId] ASC, [ExternalIdHash] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_State_ResumeWindowExpirationDate]
    ON [dbo].[OrganizationPartnershipEntitlement]([State] ASC, [ResumeWindowExpirationDate] ASC)
    WHERE [UserId] IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationPartnershipEntitlement_UserId]
    ON [dbo].[OrganizationPartnershipEntitlement]([UserId] ASC);
GO
