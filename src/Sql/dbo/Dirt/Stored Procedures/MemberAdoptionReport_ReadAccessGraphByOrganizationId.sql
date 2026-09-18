CREATE PROCEDURE [dbo].[MemberAdoptionReport_ReadAccessGraphByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;

    -- Result set 1: which members can reach which collections, directly or through a group.
    -- UNION rather than UNION ALL because a member can hold both kinds of access to one collection.
    WITH [OrganizationCollection] AS (
        SELECT
            COL.[Id]
        FROM
            [dbo].[Collection] COL
        WHERE
            COL.[OrganizationId] = @OrganizationId
    )
    SELECT
        CU.[OrganizationUserId],
        CU.[CollectionId]
    FROM
        [dbo].[CollectionUser] CU
    INNER JOIN
        [OrganizationCollection] COL ON COL.[Id] = CU.[CollectionId]
    UNION
    SELECT
        GU.[OrganizationUserId],
        CG.[CollectionId]
    FROM
        [dbo].[CollectionGroup] CG
    INNER JOIN
        [OrganizationCollection] COL ON COL.[Id] = CG.[CollectionId]
    INNER JOIN
        [dbo].[GroupUser] GU ON GU.[GroupId] = CG.[GroupId]

    -- Result set 2: which collections hold which items.
    ;WITH [OrganizationCollection] AS (
        SELECT
            COL.[Id]
        FROM
            [dbo].[Collection] COL
        WHERE
            COL.[OrganizationId] = @OrganizationId
    )
    SELECT
        CC.[CollectionId],
        CC.[CipherId]
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        [OrganizationCollection] COL ON COL.[Id] = CC.[CollectionId]
    INNER JOIN
        [dbo].[Cipher] C ON C.[Id] = CC.[CipherId]
            AND C.[OrganizationId] = @OrganizationId
            AND C.[DeletedDate] IS NULL
END
