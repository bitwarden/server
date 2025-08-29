CREATE PROCEDURE [dbo].[OrganizationReport_GetSummariesByDateRange]
    @OrganizationId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [SummaryData]
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId
        AND [RevisionDate] >= @StartDate
        AND [RevisionDate] <= @EndDate
    ORDER BY [RevisionDate] DESC
END

