CREATE PROCEDURE [dbo].[OrganizationReport_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @SummaryData NVARCHAR(MAX),
    @ReportData NVARCHAR(MAX),
    @ApplicationData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport](
        [Id],
        [OrganizationId],
        [SummaryData],
        [ReportData],
        [ApplicationData],
        [CreationDate],
        [RevisionDate],
        [ContentEncryptionKey]
    )
    VALUES (
        @Id,
        @OrganizationId,
        @SummaryData,
        @ReportData,
        @ApplicationData,
        @CreationDate,
        @RevisionDate,
        @ContentEncryptionKey
    );
