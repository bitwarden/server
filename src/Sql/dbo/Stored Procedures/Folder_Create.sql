CREATE PROCEDURE [dbo].[Folder_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    INSERT INTO [dbo].[Folder]
    (
        [Id],
        [UserId],
        [Name],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @CreationDate,
        @RevisionDate
    )
END
