CREATE PROCEDURE [dbo].[OrganizationUser_Enable]
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
        AND [Status] = -1 -- Disabled

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
END
