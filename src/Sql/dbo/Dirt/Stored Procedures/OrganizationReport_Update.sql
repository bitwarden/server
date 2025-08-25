CREATE PROCEDURE [dbo].[OrganizationReport_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ReportData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX),
    @SummaryData NVARCHAR(MAX),
    @ApplicationData NVARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
UPDATE [dbo].[OrganizationReport]
SET
    [OrganizationId] = @OrganizationId,
    [ReportData] = @ReportData,
    [CreationDate] = @CreationDate,
    [ContentEncryptionKey] = @ContentEncryptionKey,
    [SummaryData] = @SummaryData,
    [ApplicationData] = @ApplicationData,
    [RevisionDate] = @RevisionDate
WHERE [Id] = @Id;
