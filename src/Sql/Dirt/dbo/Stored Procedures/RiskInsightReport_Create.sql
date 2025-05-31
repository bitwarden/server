CREATE PROCEDURE [dbo].[RiskInsightReport_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[RiskInsightReport]( [Id],[OrganizationId],[Date],[ReportData],[CreationDate],[RevisionDate] )
    VALUES ( @Id,@OrganizationId,@Date,@ReportData,@CreationDate,@RevisionDate);
