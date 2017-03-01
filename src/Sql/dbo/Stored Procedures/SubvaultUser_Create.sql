CREATE PROCEDURE [dbo].[SubvaultUser_Create]
    @Id UNIQUEIDENTIFIER,
    @SubvaultId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Admin BIT,
    @ReadOnly BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SubvaultUser]
    (
        [Id],
        [SubvaultId],
        [UserId],
        [Admin],
        [ReadOnly],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @SubvaultId,
        @UserId,
        @Admin,
        @ReadOnly,
        @CreationDate,
        @RevisionDate
    )
END