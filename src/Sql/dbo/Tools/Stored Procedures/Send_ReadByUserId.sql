CREATE PROCEDURE [dbo].[Send_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [OrganizationId] IS NULL
        AND [UserId] = @UserId
END