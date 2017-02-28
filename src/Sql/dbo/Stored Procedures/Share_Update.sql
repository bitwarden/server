CREATE PROCEDURE [dbo].[Share_Update]
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

    UPDATE
        [dbo].[Share]
    SET
        [UserId] = @UserId,
        [SharerUserId] = @SharerUserId,
        [CipherId] = @CipherId,
        [Key] = @Key,
        [ReadOnly] = @ReadOnly,
        [Status] = @Status,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
