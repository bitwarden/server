-- OrganizationSponsorship_OrganizationDeleted
IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoringOrganizationId] = NULL
    WHERE
        [SponsoringOrganizationId] = @OrganizationId AND
        [CloudSponsor] = 0

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoredOrganizationId] = NULL
    WHERE
        [SponsoredOrganizationId] = @OrganizationId AND
        [CloudSponsor] = 0

    DELETE
    FROM
        [dbo].[OrganizationSponsorship]
    WHERE
        [CloudSponsor] = 1 AND
        ([SponsoredOrganizationId] = @OrganizationId OR
         [SponsoringOrganizationId] = @OrganizationId)
END
GO

-- OrganizationSponsorship_OrganizationUserDeleted
IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationUserDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUserDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUserDeleted]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET
        [SponsoringOrganizationUserId] = NULL
    FROM
        [dbo].[OrganizationSponsorship] OS
    WHERE
        [SponsoringOrganizationUserId] = @OrganizationUserId
END
GO

-- OrganizationSponsorship_OrganizationUsersDeleted
IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationUsersDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
    @SponsoringOrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET
        [SponsoringOrganizationUserId] = NULL
    FROM
        [dbo].[OrganizationSponsorship] OS
    INNER JOIN
        @SponsoringOrganizationUserIds I ON I.Id = OS.SponsoringOrganizationUserId
END
GO
