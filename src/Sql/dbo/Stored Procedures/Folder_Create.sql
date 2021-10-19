CREATE PROCEDURE [dbo].[Folder_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

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

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
