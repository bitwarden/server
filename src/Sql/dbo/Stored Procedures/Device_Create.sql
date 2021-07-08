CREATE PROCEDURE [dbo].[Device_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Device]
    (
        [Id],
        [UserId],
        [Name],
        [Type],
        [Identifier],
        [PushToken],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Type,
        @Identifier,
        @PushToken,
        @CreationDate,
        @RevisionDate
    )
END
