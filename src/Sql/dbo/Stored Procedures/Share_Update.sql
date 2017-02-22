CREATE PROCEDURE [dbo].[Share_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Key NVARCHAR(MAX),
    @Permissions NVARCHAR(MAX),
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Share]
    SET
        [UserId] = @UserId,
        [CipherId] = @CipherId,
        [Key] = @Key,
        [Permissions] = @Permissions,
        [Status] = @Status,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END