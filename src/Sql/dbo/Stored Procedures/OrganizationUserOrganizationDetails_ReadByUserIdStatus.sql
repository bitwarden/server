CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
        AND (@Status IS NULL OR [Status] = @Status)
END