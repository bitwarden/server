CREATE OR ALTER PROCEDURE [dbo].[Transaction_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        TOP (@Limit) *
    FROM
        [dbo].[TransactionView]
    WHERE
        [UserId] = @UserId
    ORDER BY
        [CreationDate] DESC
END
