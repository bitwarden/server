CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        UCD.[Id],
        UCD.[OrganizationId],
        UCD.[Name],
        UCD.[CreationDate],
        UCD.[RevisionDate],
        UCD.[ExternalId],
        MIN(UCD.[ReadOnly]) AS [ReadOnly],
        MIN(UCD.[HidePasswords]) AS [HidePasswords],
        MAX(UCD.[Manage]) AS [Manage],
        UCD.[DefaultUserCollectionEmail],
        UCD.[Type],
        UCD.[AccessRuleId],
        MAX(CASE WHEN AR.[Enabled] = 1 THEN 1 ELSE 0 END) AS [HasEnabledAccessRule]
    FROM
        [dbo].[UserCollectionDetails](@UserId) UCD
    LEFT JOIN
        [dbo].[AccessRule] AR ON AR.[Id] = UCD.[AccessRuleId]
    GROUP BY
        UCD.[Id],
        UCD.[OrganizationId],
        UCD.[Name],
        UCD.[CreationDate],
        UCD.[RevisionDate],
        UCD.[ExternalId],
        UCD.[DefaultUserCollectionEmail],
        UCD.[Type],
        UCD.[AccessRuleId]
END
