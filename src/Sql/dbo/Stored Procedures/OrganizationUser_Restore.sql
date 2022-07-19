CREATE PROCEDURE [dbo].[OrganizationUser_Restore]
    @Id UNIQUEIDENTIFIER,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationUser]
    SET
        [Status] = @Status
    WHERE
        [Id] = @Id
        AND [Status] = -1 -- Revoked

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
END
