CREATE PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_ReadManyExpired]
    @AsOf DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipReconciliationExportView]
    WHERE
        [ExpirationDate] <= @AsOf
END
