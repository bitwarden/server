CREATE OR ALTER PROCEDURE [dbo].[Transaction_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER,
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
        [ProviderId] = @ProviderId
        AND (@StartAfter IS NULL OR [CreationDate] < @StartAfter)
    ORDER BY
        [CreationDate] DESC
END
