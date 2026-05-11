CREATE PROCEDURE [dbo].[OrganizationReport_GetSummaryDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationId],
        [SummaryData],
        [ContentEncryptionKey],
        [RevisionDate]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END


