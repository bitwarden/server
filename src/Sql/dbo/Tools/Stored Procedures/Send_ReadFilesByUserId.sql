CREATE PROCEDURE [dbo].[Send_ReadFilesByUserId]
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
        AND [Type] = 1
END
