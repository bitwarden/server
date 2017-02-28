CREATE PROCEDURE [dbo].[Share_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @SharerUserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Key NVARCHAR(MAX),
    @ReadOnly BIT,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Share]
    (
        [Id],
        [UserId],
        [SharerUserId],
        [CipherId],
        [Key],
        [ReadOnly],
        [Status],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @SharerUserId,
        @CipherId,
        @Key,
        @ReadOnly,
        @Status,
        @CreationDate,
        @RevisionDate
    )
END
