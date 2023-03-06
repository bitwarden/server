CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadByIdsAndOrganizationId]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId UNIQUEIDENTIFIER
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
        [dbo].[Collection]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids) AND [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Folder_ReadByIdsAndUserId]
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