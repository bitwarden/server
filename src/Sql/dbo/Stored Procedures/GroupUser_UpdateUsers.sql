CREATE PROCEDURE [dbo].[GroupUser_UpdateUsers]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[Group]
        WHERE
            [Id] = @GroupId
    )

    CREATE TABLE #TempAvailableUsers
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #TempAvailableUsers
    SELECT
        [Id]
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [OrganizationId] = @OrgId

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        @GroupId,
        [Source].[Id]
    FROM
        @OrganizationUserIds AS [Source]
    WHERE
        [Source].[Id] IN (SELECT [Id] FROM #TempAvailableUsers)
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[GroupUser]
            WHERE
                [GroupId] = @GroupId
                AND [OrganizationUserId] = [Source].[Id]
        )
    
    -- Delete
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    WHERE
        GU.[GroupId] = @GroupId
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @OrganizationUserIds
            WHERE
                [Id] = GU.[OrganizationUserId]
        )

    DROP TABLE #TempAvailableUsers

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
END