CREATE PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [SummaryData]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END


