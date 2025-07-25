CREATE PROCEDURE [dbo].[OrganizationReportSummary_ReadByOrganizationReportId]
    @OrganizationReportId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
    SELECT
        *
    FROM [dbo].[OrganizationReportSummary]
    WHERE [OrganizationReportId] = @OrganizationReportId;
