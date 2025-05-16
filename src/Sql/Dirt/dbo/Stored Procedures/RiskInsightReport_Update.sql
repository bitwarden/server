CREATE PROCEDURE [dbo].[RiskInsightReport_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    UPDATE [dbo].[RiskInsightReport]
    SET [OrganizationId] = @OrganizationId,
        [Date] = @Date,
        [ReportData] = @ReportData,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id
