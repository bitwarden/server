CREATE PROCEDURE [dbo].[Transaction_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TransactionView]
    WHERE
        [UserId] = @UserId
END