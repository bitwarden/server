CREATE PROCEDURE [dbo].[MemberAdoptionReport_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;

    ;WITH [Member] AS (
        SELECT
            OU.[Id],
            OU.[UserId],
            OU.[Email]
        FROM
            [dbo].[OrganizationUser] OU
        WHERE
            OU.[OrganizationId] = @OrganizationId
            -- Adoption is only measured for Confirmed members.
            AND OU.[Status] = 2
    ),
    [MemberVaultItem] AS (
        SELECT
            C.[UserId],
            COUNT(1) AS [VaultItemCount]
        FROM
            [dbo].[Cipher] C
        WHERE
            C.[OrganizationId] IS NULL
            AND C.[DeletedDate] IS NULL
            -- Restricting to this organization's members keeps the aggregate off every other
            -- account's personal vault. A member with no [UserId] matches nothing, as before.
            AND EXISTS (SELECT 1 FROM [Member] M WHERE M.[UserId] = C.[UserId])
        GROUP BY
            C.[UserId]
    )
    SELECT
        M.[Id] AS [OrganizationUserId],
        M.[UserId],
        U.[Name],
        ISNULL(ISNULL(U.[Email], M.[Email]), '') AS [Email],
        DEV.[LastActivityDate],
        CAST(ISNULL(DEV.[HasExtension], 0) AS BIT) AS [HasExtensionInstalled],
        ISNULL(VI.[VaultItemCount], 0) AS [VaultItemCount],
        CAST(CASE WHEN SP.[Id] IS NULL THEN 0 ELSE 1 END AS BIT) AS [HasRedeemedSponsorship]
    FROM
        [Member] M
    LEFT JOIN
        [dbo].[User] U ON U.[Id] = M.[UserId]
    OUTER APPLY (
        SELECT
            MAX(D.[LastActivityDate]) AS [LastActivityDate],
            -- Chrome, Firefox, Opera, Edge, Vivaldi and Safari browser extensions
            MAX(CASE WHEN D.[Type] IN (2, 3, 4, 5, 19, 20) THEN 1 ELSE 0 END) AS [HasExtension]
        FROM
            [dbo].[Device] D
        WHERE
            D.[UserId] = M.[UserId]
    ) DEV
    LEFT JOIN
        [MemberVaultItem] VI ON VI.[UserId] = M.[UserId]
    OUTER APPLY (
        SELECT TOP 1
            OS.[Id]
        FROM
            [dbo].[OrganizationSponsorship] OS
        WHERE
            OS.[SponsoringOrganizationUserID] = M.[Id]
            AND OS.[SponsoredOrganizationId] IS NOT NULL
    ) SP
    ORDER BY
        [Email] ASC,
        M.[Id] ASC
    -- The remaining per-member OUTER APPLYs make this CPU-bound rather than I/O-bound, and its
    -- parallel plan scales negatively: it burns more CPU across workers than it saves in elapsed time.
    OPTION (MAXDOP 1)
END
