CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetMetrics]
    @OrganizationId UNIQUEIDENTIFIER,
    @minDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT *
    FROM dbo.OrganizationReportMetricsView
    WHERE OrganizationId = @OrganizationId
        AND RevisionDate >= @minDate
END
