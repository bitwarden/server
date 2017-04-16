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

    MERGE
        [dbo].[SubvaultCipher] AS [Target]
    USING 
        @SubvaultIds AS [Source]
    ON
        [Target].[SubvaultId] = [Source].[Id]
        AND [Target].[CipherId] = @Id
    WHEN NOT MATCHED BY TARGET THEN
        INSERT VALUES
        (
            [Source].[Id],
            @Id
        )
    WHEN NOT MATCHED BY SOURCE
    AND [Target].[CipherId] = @Id THEN
        DELETE
    ;
END