CREATE PROCEDURE [dbo].[Folder_ReadByIdsAndUserId]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
        BEGIN
            RETURN(-1)
        END

    SELECT
        *
    FROM
        [dbo].[Folder]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids) AND [UserId] = @UserId
END