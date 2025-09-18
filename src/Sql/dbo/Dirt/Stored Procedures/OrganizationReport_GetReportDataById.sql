CREATE PROCEDURE [dbo].[OrganizationReport_GetReportDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [ReportData]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END

