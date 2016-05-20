CREATE PROCEDURE [dbo].[History_Create]
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Event TINYINT,
    @Date DATETIME2(7)
AS
BEGIN
    INSERT INTO [dbo].[History]
    (
        [UserId],
        [CipherId],
        [Event],
        [Date]
    )
    VALUES
    (
        @UserId,
        @CipherId,
        @Event,
        @Date
    )
END
