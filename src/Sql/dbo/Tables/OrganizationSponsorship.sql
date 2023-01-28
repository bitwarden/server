CREATE TABLE [dbo].[OrganizationSponsorship] (
    [Id]                            UNIQUEIDENTIFIER NOT NULL,
    [SponsoringOrganizationId]      UNIQUEIDENTIFIER NULL,
    [SponsoringOrganizationUserID]  UNIQUEIDENTIFIER NOT NULL,
    [SponsoredOrganizationId]       UNIQUEIDENTIFIER NULL,
    [FriendlyName]                  NVARCHAR(256)    NULL,
    [OfferedToEmail]                NVARCHAR (256)   NULL,
    [PlanSponsorshipType]           TINYINT          NULL,
    [ToDelete]                      BIT              DEFAULT (0) NOT NULL,
    [LastSyncDate]                  DATETIME2 (7)    NULL,
    [ValidUntil]                    DATETIME2 (7)    NULL,
    CONSTRAINT [PK_OrganizationSponsorship] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationSponsorship_SponsoringOrg] FOREIGN KEY ([SponsoringOrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_OrganizationSponsorship_SponsoredOrg] FOREIGN KEY ([SponsoredOrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
);


GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationId]
    ON [dbo].[OrganizationSponsorship]([SponsoringOrganizationId] ASC)
    WHERE [SponsoringOrganizationId] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationUserId]
    ON [dbo].[OrganizationSponsorship]([SponsoringOrganizationUserID] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_OfferedToEmail]
    ON [dbo].[OrganizationSponsorship]([OfferedToEmail] ASC)
    WHERE [OfferedToEmail] IS NOT NULL;

GO
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoredOrganizationID]
    ON [dbo].[OrganizationSponsorship]([SponsoredOrganizationId] ASC)
    WHERE [SponsoredOrganizationId] IS NOT NULL;
