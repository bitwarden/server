CREATE PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [SummaryData]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END


