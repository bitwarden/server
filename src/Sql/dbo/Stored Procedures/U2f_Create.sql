CREATE PROCEDURE [dbo].[U2f_Create]
    @Id INT,
    @UserId UNIQUEIDENTIFIER,
    @KeyHandle VARCHAR(50),
    @Challenge VARCHAR(50),
    @AppId VARCHAR(50),
    @Version VARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[U2f]
    (
        [Id],
        [UserId],
        [KeyHandle],
        [Challenge],
        [AppId],
        [Version],
        [CreationDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @KeyHandle,
        @Challenge,
        @AppId,
        @Version,
        @CreationDate
    )
END