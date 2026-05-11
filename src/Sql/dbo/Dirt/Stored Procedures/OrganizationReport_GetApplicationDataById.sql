CREATE PROCEDURE [dbo].[OrganizationReport_GetApplicationDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationId],
        [ApplicationData],
        [ContentEncryptionKey],
        [RevisionDate]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END

