CREATE PROCEDURE [dbo].[Transaction_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Limit INT,
    @StartAfter DATETIME2 = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP (@Limit) *
    FROM [dbo].[TransactionView]
    WHERE
        [OrganizationId] = @OrganizationId
      AND (@StartAfter IS NULL OR [CreationDate] < @StartAfter)
    ORDER BY
        [CreationDate] DESC
END
