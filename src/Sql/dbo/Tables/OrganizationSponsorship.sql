CREATE TABLE [dbo].[OrganizationSponsorship] (
    [Id]                            UNIQUEIDENTIFIER NOT NULL,
    [InstallationId]                UNIQUEIDENTIFIER NULL,
    [SponsoringOrganizationId]      UNIQUEIDENTIFIER NOT NULL,
    [SponsorginOrganizationUserID]  UNIQUEIDENTIFIER NOT NULL,
    [SponsoredOrganizationId]       UNIQUEIDENTIFIER NULL,
    [OfferedToEmail]                NVARCHAR (256)   NULL,
    [CloudSponsor]                  BIT              NULL,
    [LastSyncDate]                  DATETIME2 (7)    NULL,
    [TimesRenewedWithoutValidation] TINYINT          DEFAULT 0,
    [SponsorshipLapsedDate]         DATETIME2 (7)    NULL,
    CONSTRAINT [PK_OrganizationSponsorship] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationSponsorship_InstallationId] FOREIGN KEY ([InstallationId]) REFERENCES [dbo].[Installation] ([Id]),
    CONSTRAINT [FK_OrganizationSponsorship_SponsoringOrg] FOREIGN KEY ([SponsoringOrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_OrganizationSponsorship_SponsoredOrg] FOREIGN KEY ([SponsoredOrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
);


GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_InstallationId]
    ON [dbo].[Organization]([Id] ASC, [InstallationId] ASC)
    WHERE [InstallationId] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationId]
    ON [dbo].[Organization]([Id] ASC, [SponsoringOrganizationId] ASC)

GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationUserId]
    ON [dbo].[Organization]([Id] ASC, [SponsorginOrganizationUserID] ASC)

GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_OfferedToEmail]
    ON [dbo].[Organization]([Id] ASC, [OfferedToEmail] ASC)
    WHERE [OfferedToEmail] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoredOrganizationID]
    ON [dbo].[Organization]([Id] ASC, [SponsoredOrganizationId] ASC)
    WHERE [SponsoredOrganizationId] IS NOT NULL;

