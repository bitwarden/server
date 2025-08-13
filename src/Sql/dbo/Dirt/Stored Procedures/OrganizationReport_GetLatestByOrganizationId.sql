CREATE PROCEDURE [dbo].[OrganizationReport_GetLatestByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [ReportData],
        [CreationDate],
        [ContentEncryptionKey],
        [SummaryData],
        [ApplicationData],
        [RevisionDate]
    FROM [dbo].[OrganizationReport]
    WHERE [OrganizationId] = @OrganizationId
    ORDER BY [RevisionDate] DESC
END

