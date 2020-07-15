CREATE OR REPLACE PROCEDURE groupuser_updateusers(par_groupid uuid, par_organization_userids guididarray)
 LANGUAGE plpgsql
AS $procedure$
/*
[7916 - Severity CRITICAL - Current MERGE statement can not be emulated by INSERT ON CONFLICT usage. To achieve the effect of a MERGE statement, use separate INSERT, DELETE, and UPDATE statements or by cursor usage.]
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

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrgId
END
*/
BEGIN
END;
$procedure$
;
