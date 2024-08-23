CREATE OR ALTER PROCEDURE [dbo].[Transaction_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @Limit INT,
    @StartAfter DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        TOP (@Limit) *
    FROM
        [dbo].[TransactionView]
    WHERE
        [UserId] = @UserId
        AND (@StartAfter IS NULL OR [CreationDate] < @StartAfter)
    ORDER BY
        [CreationDate] DESC
END
