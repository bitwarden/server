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
        -- ReadOnly and HidePasswords are the Edit (2) and ViewPassword (1) parts of the highest rank, inverted
        1 - (MAX(R.[Rank]) / 2) % 2 AS [ReadOnly],
        1 - MAX(R.[Rank]) % 2 AS [HidePasswords],
        MAX(UCD.[Manage]) AS [Manage],
        UCD.[DefaultUserCollectionEmail],
        UCD.[Type],
        UCD.[AccessRuleId],
        MAX(CASE WHEN AR.[Enabled] = 1 THEN 1 ELSE 0 END) AS [HasEnabledAccessRule]
    FROM
        [dbo].[UserCollectionDetails](@UserId) UCD
    LEFT JOIN
        [dbo].[AccessRule] AR ON AR.[Id] = UCD.[AccessRuleId]
    -- Rank orders a grant by Manage, then Edit, then ViewPassword, so the highest permission level wins
    CROSS APPLY (
        SELECT UCD.[Manage] * 4 + (1 - UCD.[ReadOnly]) * 2 + (1 - UCD.[HidePasswords]) AS [Rank]
    ) R
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
