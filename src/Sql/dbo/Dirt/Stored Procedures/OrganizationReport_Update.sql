CREATE PROCEDURE [dbo].[OrganizationReport_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    
    UPDATE [dbo].[OrganizationReport]
    SET [OrganizationId] = @OrganizationId,
        [Date] = @Date,
        [ReportData] = @ReportData,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id;
