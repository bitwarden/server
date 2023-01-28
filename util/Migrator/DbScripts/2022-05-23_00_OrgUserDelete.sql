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
        [ToDelete] = 1
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
        [ToDelete] = 1
    FROM
        [dbo].[OrganizationSponsorship] OS
    INNER JOIN
        @SponsoringOrganizationUserIds I ON I.Id = OS.SponsoringOrganizationUserId
END
GO
