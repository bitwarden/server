CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT,
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