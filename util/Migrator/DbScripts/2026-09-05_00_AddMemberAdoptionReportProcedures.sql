-- Member adoption report: one row per confirmed organization member.
CREATE OR ALTER PROCEDURE [dbo].[MemberAdoptionReport_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;

    SELECT
        OU.[Id] AS [OrganizationUserId],
        OU.[UserId],
        U.[Name],
        ISNULL(ISNULL(U.[Email], OU.[Email]), '') AS [Email],
        LA.[LastActivityDate],
        CAST(CASE WHEN EXT.[Id] IS NULL THEN 0 ELSE 1 END AS BIT) AS [HasExtensionInstalled],
        ISNULL(VI.[VaultItemCount], 0) AS [VaultItemCount],
        ISNULL(SI.[SharedItemCount], 0) AS [SharedItemCount],
        CAST(CASE WHEN SP.[Id] IS NULL THEN 0 ELSE 1 END AS BIT) AS [HasRedeemedSponsorship]
    FROM
        [dbo].[OrganizationUser] OU
    LEFT JOIN
        [dbo].[User] U ON U.[Id] = OU.[UserId]
    OUTER APPLY (
        SELECT
            MAX(D.[LastActivityDate]) AS [LastActivityDate]
        FROM
            [dbo].[Device] D
        WHERE
            D.[UserId] = OU.[UserId]
    ) LA
    OUTER APPLY (
        SELECT TOP 1
            D.[Id]
        FROM
            [dbo].[Device] D
        WHERE
            D.[UserId] = OU.[UserId]
            AND D.[Type] IN (2, 3, 4, 5, 19, 20) -- Chrome, Firefox, Opera, Edge, Vivaldi and Safari browser extensions
    ) EXT
    OUTER APPLY (
        SELECT
            COUNT(1) AS [VaultItemCount]
        FROM
            [dbo].[Cipher] C
        WHERE
            C.[UserId] = OU.[UserId]
            AND C.[OrganizationId] IS NULL
            AND C.[DeletedDate] IS NULL
    ) VI
    OUTER APPLY (
        SELECT
            COUNT(DISTINCT CC.[CipherId]) AS [SharedItemCount]
        FROM
            [dbo].[CollectionCipher] CC
        INNER JOIN
            [dbo].[Collection] COL ON COL.[Id] = CC.[CollectionId]
                AND COL.[OrganizationId] = @OrganizationId
        INNER JOIN
            [dbo].[Cipher] OC ON OC.[Id] = CC.[CipherId]
                AND OC.[OrganizationId] = @OrganizationId
                AND OC.[DeletedDate] IS NULL
        WHERE
            EXISTS (
                SELECT 1
                FROM [dbo].[CollectionUser] CU
                WHERE CU.[CollectionId] = CC.[CollectionId]
                    AND CU.[OrganizationUserId] = OU.[Id]
            )
            OR EXISTS (
                SELECT 1
                FROM [dbo].[CollectionGroup] CG
                INNER JOIN [dbo].[GroupUser] GU ON GU.[GroupId] = CG.[GroupId]
                WHERE CG.[CollectionId] = CC.[CollectionId]
                    AND GU.[OrganizationUserId] = OU.[Id]
            )
    ) SI
    OUTER APPLY (
        SELECT TOP 1
            OS.[Id]
        FROM
            [dbo].[OrganizationSponsorship] OS
        WHERE
            OS.[SponsoringOrganizationUserID] = OU.[Id]
            AND OS.[SponsoredOrganizationId] IS NOT NULL
    ) SP
    WHERE
        OU.[OrganizationId] = @OrganizationId
        -- Adoption is only measured for Confirmed members.
        AND OU.[Status] = 2
    ORDER BY
        [Email] ASC,
        OU.[Id] ASC
END
GO
