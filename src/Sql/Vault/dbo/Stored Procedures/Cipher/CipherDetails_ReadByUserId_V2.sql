CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId_V2]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
END
