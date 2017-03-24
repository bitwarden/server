CREATE PROCEDURE [dbo].[SubvaultUser_ReadCanEditByCipherIdUserId]
    @UserId UNIQUEIDENTIFIER,
    @CipherId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [dbo].[UserCanEditCipher](@UserId, @CipherId)
END
