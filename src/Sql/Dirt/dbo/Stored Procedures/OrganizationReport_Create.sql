CREATE PROCEDURE [dbo].[OrganizationReport_Create]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport]( [Id],[OrganizationId],[Date],[ReportData],[CreationDate],[RevisionDate] )
    VALUES ( @Id,@OrganizationId,@Date,@ReportData,@CreationDate,@RevisionDate);
