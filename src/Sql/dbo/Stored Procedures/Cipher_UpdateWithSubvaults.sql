CREATE PROCEDURE [dbo].[Cipher_UpdateWithSubvaults]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data NVARCHAR(MAX),
    @Favorites NVARCHAR(MAX),
    @Folders NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @SubvaultIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Cipher]
    SET
        [UserId] = NULL,
        [OrganizationId] = @OrganizationId,
        [Data] = @Data,
        [RevisionDate] = @RevisionDate
        -- No need to update CreationDate, Favorites, Folders, or Type since that data will not change
    WHERE
        [Id] = @Id

    ;WITH [AvailableSubvaultsCTE] AS(
        SELECT
            S.[Id]
        FROM
            [dbo].[Subvault] S
        INNER JOIN
            [Organization] O ON O.[Id] = S.[OrganizationId]
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[OrganizationId] = O.[Id] AND OU.[UserId] = @UserId
        LEFT JOIN
            [dbo].[SubvaultUser] SU ON OU.[AccessAllSubvaults] = 0 AND SU.[SubvaultId] = S.[Id] AND SU.[OrganizationUserId] = OU.[Id]
        WHERE
            O.[Id] = @OrganizationId
            AND O.[Enabled] = 1
            AND OU.[Status] = 2 -- Confirmed
            AND (OU.[AccessAllSubvaults] = 1 OR SU.[ReadOnly] = 0)
    )
    INSERT INTO [dbo].[SubvaultCipher]
    (
        [SubvaultId],
        [CipherId]
    )
    SELECT
        [Id],
        @Id
    FROM
        @SubvaultIds
    WHERE
        [Id] IN (SELECT [Id] FROM [AvailableSubvaultsCTE])
END