CREATE PROCEDURE [dbo].[GroupUser_Delete]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @RevisionDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

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

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [GroupId] = @GroupId
        AND [OrganizationUserId] = @OrganizationUserId

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END