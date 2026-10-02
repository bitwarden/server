CREATE PROCEDURE [dbo].[GroupUser_UpdateGroups]
    @OrganizationUserId UNIQUEIDENTIFIER,
    @GroupIds AS [dbo].[GuidIdArray] READONLY,
    @RevisionDate DATETIME2(7) = NULL
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

    -- Bump RevisionDate on all affected groups (old + new)
    IF @RevisionDate IS NOT NULL
    BEGIN
        ;WITH [AffectedGroupsCTE] AS (
            SELECT
                [Id]
            FROM
                @GroupIds

            UNION

            SELECT
                GU.[GroupId]
            FROM
                [dbo].[GroupUser] GU
            WHERE
                GU.[OrganizationUserId] = @OrganizationUserId
        )
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        WHERE
            G.[OrganizationId] = @OrgId
            AND G.[Id] IN (SELECT [Id] FROM [AffectedGroupsCTE])
    END

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        [Source].[Id],
        @OrganizationUserId
    FROM
        @GroupIds [Source]
    INNER JOIN
        [dbo].[Group] G ON G.[Id] = [Source].[Id] AND G.[OrganizationId] = @OrgId
    WHERE
        NOT EXISTS (
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END