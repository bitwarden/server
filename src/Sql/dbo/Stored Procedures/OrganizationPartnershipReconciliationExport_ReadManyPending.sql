CREATE PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_ReadManyPending]
AS
BEGIN
    SET NOCOUNT ON

    -- Status 0 is PartnershipReconciliationExportStatus.Pending
    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipReconciliationExportView]
    WHERE
        [Status] = 0
    ORDER BY
        [CreationDate] ASC
END
