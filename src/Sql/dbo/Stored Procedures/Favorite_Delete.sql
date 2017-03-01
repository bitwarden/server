CREATE PROCEDURE [dbo].[Favorite_Delete]
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Favorite]
    WHERE
        [UserId] = @UserId
        AND [CipherId] = @CipherId
END