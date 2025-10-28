CREATE PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM [dbo].[OrganizationReportView]
    WHERE [OrganizationId] = @OrganizationId
    ORDER BY [RevisionDate] DESC
END
