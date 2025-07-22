CREATE PROCEDURE [dbo].[OrganizationReportSummary_Update]
    @Id                   UNIQUEIDENTIFIER,
    @OrganizationReportId UNIQUEIDENTIFIER,
    @SummaryDetails       VARCHAR(MAX),
    @ContentEncryptionKey VARCHAR(MAX),
    @UpdateDate           DATETIME2(7)
AS
    SET NOCOUNT ON;
    UPDATE [dbo].[OrganizationReportSummary]
    SET
        [OrganizationReportId] = @OrganizationReportId,
        [SummaryDetails] = @SummaryDetails,
        [ContentEncryptionKey] = @ContentEncryptionKey,
        [UpdateDate] = @UpdateDate
    WHERE [Id] = @Id;
