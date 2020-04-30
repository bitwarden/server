CREATE PROCEDURE [dbo].[GroupUser_UpdateGroups]
    @OrganizationUserId UNIQUEIDENTIFIER,
    @GroupIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @OrgId UNIQUEIDENTIFIER = (
        SELECT TOP 1
            [OrganizationId]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [Id] = @OrganizationUserId
    )

    CREATE TABLE #TempAvailableGroups
    (
        [Id] UNIQUEIDENTIFIER NOT NULL
    )

    INSERT INTO #TempAvailableGroups
    SELECT
        [Id]
    FROM
        [dbo].[Group]
    WHERE
        [OrganizationId] = @OrgId

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        [Source].[Id],
        @OrganizationUserId
    FROM
        @GroupIds AS [Source]
    WHERE
        [Source].[Id] IN (SELECT [Id] FROM #TempAvailableGroups)
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[GroupUser]
            WHERE
                [OrganizationUserId] = @OrganizationUserId
                AND [GroupId] = [Source].[Id]
        )
    
    -- Delete
    DELETE
        GU
    FROM
        [dbo].[GroupUser] GU
    WHERE
        GU.[OrganizationUserId] = @OrganizationUserId
        AND NOT EXISTS (
            SELECT
                1
            FROM
                @GroupIds
            WHERE
                [Id] = GU.[GroupId]
        )

    DROP TABLE #TempAvailableGroups

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END