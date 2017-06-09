CREATE PROCEDURE [dbo].[CipherDetails_ReadByTypeUserId]
    @Type TINYINT,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Type] = @Type
END