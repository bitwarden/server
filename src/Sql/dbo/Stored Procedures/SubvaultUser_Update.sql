CREATE PROCEDURE [dbo].[SubvaultUser_Update]
    @Id UNIQUEIDENTIFIER,
    @SubvaultId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Key VARCHAR(MAX),
    @Admin BIT,
    @ReadOnly BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SubvaultUser]
    SET
        [SubvaultId] = @SubvaultId,
        [UserId] = @UserId,
        [Key] = @Key,
        [Admin] = @Admin,
        [ReadOnly] = @ReadOnly,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END