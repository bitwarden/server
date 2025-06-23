CREATE PROCEDURE [dbo].[OrganizationReport_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId;
