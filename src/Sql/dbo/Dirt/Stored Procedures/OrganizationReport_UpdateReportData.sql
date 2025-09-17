CREATE PROCEDURE [dbo].[OrganizationReport_UpdateReportData]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[OrganizationReport]
    SET
        [ReportData] = @ReportData,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id
      AND [OrganizationId] = @OrganizationId;
END
