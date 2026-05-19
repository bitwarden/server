CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_ReadLatestByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId
        AND JSON_VALUE([ReportFile], '$.Validated') = 'true'
    ORDER BY [RevisionDate] DESC
END
GO
