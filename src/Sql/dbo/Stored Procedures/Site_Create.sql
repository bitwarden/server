CREATE PROCEDURE [dbo].[Site_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @FolderId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @Uri NVARCHAR(MAX),
    @Username NVARCHAR(MAX),
    @Password NVARCHAR(MAX),
    @Notes NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    INSERT INTO [dbo].[Site]
    (
        [Id],
        [UserId],
        [FolderId],
        [Name],
        [Uri],
        [Username],
        [Password],
        [Notes],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @FolderId,
        @Name,
        @Uri,
        @Username,
        @Password,
        @Notes,
        @CreationDate,
        @RevisionDate
    )
END
