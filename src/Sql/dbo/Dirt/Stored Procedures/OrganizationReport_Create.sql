CREATE PROCEDURE [dbo].[OrganizationReport_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Date DATETIME2(7),
    @SummaryData NVARCHAR(MAX),
    @ReportData NVARCHAR(MAX),
    @ApplicationData NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @ContentEncryptionKey VARCHAR(MAX)
AS
    SET NOCOUNT ON;

    INSERT INTO [dbo].[OrganizationReport](
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [ContentEncryptionKey]
    )
    VALUES (
        @Id,
        @OrganizationId,
        @Date,
        @ReportData,
        @CreationDate,
        @ContentEncryptionKey
    );
