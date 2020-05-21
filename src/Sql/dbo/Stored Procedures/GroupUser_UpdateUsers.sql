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

    -- Insert
    INSERT INTO
        [dbo].[GroupUser]
    SELECT
        @GroupId,
        [Source].[Id]
    FROM
        @OrganizationUserIds AS [Source]
    INNER JOIN
        [dbo].[OrganizationUser] OU ON [Source].[Id] = OU.[Id] AND OU.[OrganizationId] = @OrgId
    WHERE
        NOT EXISTS (
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
END