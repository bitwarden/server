CREATE PROCEDURE [dbo].[OrganizationUser_ReadByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [Id] = @Id
        AND [UserId] = @UserId
END