IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND [OrganizationId] = @OrganizationId
        AND (@Status IS NULL OR [Status] = @Status)
END
GO
