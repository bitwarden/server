CREATE PROCEDURE [dbo].[GroupUser_AddUsers]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserIds AS [dbo].[GuidIdArray] READONLY,
    @RevisionDate DATETIME2(7) = NULL
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

    -- Bump RevisionDate on the affected group
    IF @RevisionDate IS NOT NULL
    BEGIN
        UPDATE
            G
        SET
            G.[RevisionDate] = @RevisionDate
        FROM
            [dbo].[Group] G
        WHERE
            G.[Id] = @GroupId
    END

    -- Insert
    INSERT INTO
        [dbo].[GroupUser] (GroupId, OrganizationUserId)
    SELECT DISTINCT
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
END
