CREATE OR ALTER PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @FilterByValidated BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId
        AND (
            @FilterByValidated = 0
            OR JSON_VALUE([ReportFile], '$.Validated') = 'true'
        )
    ORDER BY [RevisionDate] DESC
END
GO
