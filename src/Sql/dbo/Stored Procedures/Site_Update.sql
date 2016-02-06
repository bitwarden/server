CREATE PROCEDURE [dbo].[Site_Update]
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
    UPDATE [dbo].[Site]
    SET
        [UserId] = @UserId,
        [FolderId] = @FolderId,
        [Name] = @Name,
        [Uri] = @Uri,
        [Username] = @Username,
        [Password] = @Password,
        [Notes] = @Notes,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
