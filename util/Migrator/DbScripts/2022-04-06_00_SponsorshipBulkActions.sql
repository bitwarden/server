-- Create OrganizationSponsorshipType
IF NOT EXISTS (
    SELECT
        *
    FROM
        sys.types
    WHERE 
        [Name] = 'OrganizationSponsorshipType' AND
        is_user_defined = 1
)
BEGIN
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
END
GO

-- OrganizationSponsorship_CreateMany
IF OBJECT_ID('[dbo].[OrganizationSponsorship_CreateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_CreateMany]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_CreateMany]
    @OrganizationSponsorshipsInput [dbo].[OrganizationSponsorshipType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
        [Id],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [SponsoredOrganizationId],
        [FriendlyName],
        [OfferedToEmail],
        [PlanSponsorshipType],
        [ToDelete],
        [LastSyncDate],
        [ValidUntil]
    )
    SELECT
        OS.[Id],
        OS.[SponsoringOrganizationId],
        OS.[SponsoringOrganizationUserID],
        OS.[SponsoredOrganizationId],
        OS.[FriendlyName],
        OS.[OfferedToEmail],
        OS.[PlanSponsorshipType],
        OS.[ToDelete],
        OS.[LastSyncDate],
        OS.[ValidUntil]
    FROM
        @OrganizationSponsorshipsInput OS
END
GO

-- OrganizationSponsorship_UpdateMany
IF OBJECT_ID('[dbo].[OrganizationSponsorship_UpdateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_UpdateMany]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_UpdateMany]
    @OrganizationSponsorshipsInput [dbo].[OrganizationSponsorshipType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET 
        [Id] = OSI.[Id],
        [SponsoringOrganizationId] = OSI.[SponsoringOrganizationId],
        [SponsoringOrganizationUserID] = OSI.[SponsoringOrganizationUserID],
        [SponsoredOrganizationId] = OSI.[SponsoredOrganizationId],
        [FriendlyName] = OSI.[FriendlyName],
        [OfferedToEmail] = OSI.[OfferedToEmail],
        [PlanSponsorshipType] = OSI.[PlanSponsorshipType],
        [ToDelete] = OSI.[ToDelete],
        [LastSyncDate] = OSI.[LastSyncDate],
        [ValidUntil] = OSI.[ValidUntil]
    FROM
        [dbo].[OrganizationSponsorship] OS
    INNER JOIN
        @OrganizationSponsorshipsInput OSI ON OS.Id = OSI.Id
END
GO

-- OrganizationSponsorship_DeleteByIds
IF OBJECT_ID('[dbo].[OrganizationSponsorship_DeleteByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_DeleteByIds]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
        BEGIN
            BEGIN TRANSACTION OrgSponsorship_DeleteMany

            DELETE TOP(@BatchSize) OS
            FROM
                [dbo].[OrganizationSponsorship] OS
            INNER JOIN
                @Ids I ON I.Id = OS.Id

            SET @BatchSize = @@ROWCOUNT

            COMMIT TRANSACTION OrgSponsorship_DeleteMany
        END
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorship_DeleteExpired]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_DeleteExpired]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_DeleteExpired]
    @ValidUntilBeforeDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[OrganizationSponsorship]
        WHERE
            [ValidUntil] < @ValidUntilBeforeDate

        SET @BatchSize = @@ROWCOUNT
    END
END
GO

-- OrganizationSponsorship_ReadBySponsoringOrganizationId
IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationId]
    @SponsoringOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoringOrganizationId] = @SponsoringOrganizationId
END
GO