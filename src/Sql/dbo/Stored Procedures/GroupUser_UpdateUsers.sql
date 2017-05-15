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

    ;WITH [AvailableUsersCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[OrganizationUser]
        WHERE
            [OrganizationId] = @OrgId
    )
    MERGE
        [dbo].[GroupUser] AS [Target]
    USING 
        @OrganizationUserIds AS [Source]
    ON
        [Target].[GroupId] = @GroupId
        AND [Target].[OrganizationUserId] = [Source].[Id]
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        INSERT VALUES
        (
            @GroupId,
            [Source].[Id]
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[GroupId] = @GroupId
    AND [Target].[OrganizationUserId] IN (SELECT [Id] FROM [AvailableUsersCTE]) THEN
        DELETE
    ;

    -- TODO: Bump account revision date for all @OrganizationUserIds
END