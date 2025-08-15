CREATE PROCEDURE [dbo].[OrganizationReport_UpdateReportData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[OrganizationReport]
    SET
        [ReportData] = @ReportData,
        [RevisionDate] = GETUTCDATE()
    WHERE [Id] = @Id
      AND [OrganizationId] = @OrganizationId;
END