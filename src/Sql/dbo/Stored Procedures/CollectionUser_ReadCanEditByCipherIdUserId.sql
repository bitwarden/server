CREATE PROCEDURE [dbo].[CollectionUser_ReadCanEditByCipherIdUserId]
    @UserId UNIQUEIDENTIFIER,
    @CipherId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [dbo].[UserCanEditCipher](@UserId, @CipherId)
END