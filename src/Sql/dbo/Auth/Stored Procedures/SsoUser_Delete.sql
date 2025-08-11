CREATE PROCEDURE [dbo].[SsoUser_Delete]
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [UserId] = @UserId
        AND [OrganizationId] = @OrganizationId
END