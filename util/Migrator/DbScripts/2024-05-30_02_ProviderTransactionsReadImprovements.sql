CREATE OR ALTER PROCEDURE [dbo].[Transaction_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        TOP (@Limit) *
    FROM
        [dbo].[TransactionView]
    WHERE
        [ProviderId] = @ProviderId
    ORDER BY
        [CreationDate] DESC
END
