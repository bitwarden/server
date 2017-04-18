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
            SU.SubvaultId
        FROM
            [dbo].[SubvaultUser] SU
        INNER JOIN
            [dbo].[OrganizationUser] OU ON OU.[Id] = SU.[OrganizationUserId]
        INNER JOIN
            [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
        WHERE
            OU.[UserId] = @UserId
            AND SU.[ReadOnly] = 0
            AND OU.[Status] = 2 -- Confirmed
            AND O.[Enabled] = 1
    )
    INSERT INTO [dbo].[SubvaultCipher]
    (
        [SubvaultId],
        [CipherId]
    )
    SELECT
        Id,
        @Id
    FROM
        @SubvaultIds
    WHERE
        Id IN (SELECT SubvaultId FROM [AvailableSubvaultsCTE])
END