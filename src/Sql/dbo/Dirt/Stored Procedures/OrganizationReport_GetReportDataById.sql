CREATE PROCEDURE [dbo].[OrganizationReport_GetReportDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationId],
        [ReportData],
        [ContentEncryptionKey],
        [RevisionDate]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END

