CREATE PROCEDURE [dbo].[Device_Update]
    @Id UNIQUEIDENTIFIER,
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

    UPDATE
        [dbo].[Device]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [Type] = @Type,
        [Identifier] = @Identifier,
        [PushToken] = @PushToken,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END