CREATE OR ALTER PROCEDURE [dbo].[Transaction_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @Limit INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        TOP (@Limit) *
    FROM
        [dbo].[TransactionView]
    WHERE
        [OrganizationId] = @OrganizationId
    ORDER BY
        [CreationDate] DESC
END
