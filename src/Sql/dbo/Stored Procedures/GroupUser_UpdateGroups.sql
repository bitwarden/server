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