CREATE PROCEDURE [dbo].[Collection_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Columns are qualified with UCD because the [AccessRule] join below also has [Id],
    -- [OrganizationId] and [Name].
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
        -- Whether the collection is governed by an access rule that is currently switched on.
        -- The rule row is functionally determined by [AccessRuleId], which is in the GROUP BY, so
        -- MAX() picks the one value the group has rather than aggregating across rules.
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
