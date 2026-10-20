CREATE PROCEDURE [dbo].[OrganizationPartnershipReconciliationExport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationPartnershipReconciliationExport]
    WHERE
        [Id] = @Id
END
