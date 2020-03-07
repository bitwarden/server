CREATE PROCEDURE [dbo].[Transaction_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[TransactionView]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @OrganizationId
END