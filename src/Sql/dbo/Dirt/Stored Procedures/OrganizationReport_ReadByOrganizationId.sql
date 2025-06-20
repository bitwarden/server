CREATE PROCEDURE [dbo].[OrganizationReport_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[OrganizationReport]
    WHERE [OrganizationId] = @OrganizationId;
