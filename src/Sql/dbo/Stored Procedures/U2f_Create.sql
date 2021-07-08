CREATE PROCEDURE [dbo].[U2f_Create]
    @Id INT OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @KeyHandle VARCHAR(200),
    @Challenge VARCHAR(200),
    @AppId VARCHAR(50),
    @Version VARCHAR(20),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[U2f]
    (
        [UserId],
        [KeyHandle],
        [Challenge],
        [AppId],
        [Version],
        [CreationDate]
    )
    VALUES
    (
        @UserId,
        @KeyHandle,
        @Challenge,
        @AppId,
        @Version,
        @CreationDate
    )

    SET @Id = (SELECT SCOPE_IDENTITY())
END
