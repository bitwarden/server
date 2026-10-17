CREATE PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipReconciliationExportView]
    WHERE
        [Id] = @Id
END
