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

    ;WITH [AvailableGroupsCTE] AS(
        SELECT
            [Id]
        FROM
            [dbo].[Group]
        WHERE
            [OrganizationId] = @OrgId
    )
    MERGE
        [dbo].[GroupUser] AS [Target]
    USING 
        @GroupIds AS [Source]
    ON
        [Target].[GroupId] = [Source].[Id]
        AND [Target].[OrganizationUserId] = @OrganizationUserId
    WHEN NOT MATCHED BY TARGET
    AND [Source].[Id] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        INSERT VALUES
        (
            [Source].[Id],
            @OrganizationUserId
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[OrganizationUserId] = @OrganizationUserId
    AND [Target].[GroupId] IN (SELECT [Id] FROM [AvailableGroupsCTE]) THEN
        DELETE
    ;
END