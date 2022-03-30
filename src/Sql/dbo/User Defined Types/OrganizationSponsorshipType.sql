CREATE TYPE [dbo].[OrganizationSponsorshipType] AS TABLE(
    [Id] UNIQUEIDENTIFIER,
    [SponsoringOrganizationId] UNIQUEIDENTIFIER,
    [SponsoringOrganizationUserID] UNIQUEIDENTIFIER,
    [SponsoredOrganizationId] UNIQUEIDENTIFIER,
    [FriendlyName] NVARCHAR(256),
    [OfferedToEmail] VARCHAR(256),
    [PlanSponsorshipType] TINYINT,
    [LastSyncDate] DATETIME2(7),
    [ValidUntil] DATETIME2(7),
    [ToDelete] BIT
)