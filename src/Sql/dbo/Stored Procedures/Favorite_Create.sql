CREATE PROCEDURE [dbo].[Favorite_Create]
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Favorite]
    (
        [UserId],
        [CipherId]
    )
    VALUES
    (
        @UserId,
        @CipherId
    )
END