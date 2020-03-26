CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @Deleted BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        (@Deleted = 1 AND [DeletedDate] IS NOT NULL)
        OR (@Deleted = 0 AND [DeletedDate] IS NULL)
END