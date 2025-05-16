CREATE PROCEDURE [dbo].[RiskInsightReport_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @OrganizationId IS NULL
       THROW 50000, 'OrganizationId cannot be null', 1;

    SELECT
        [Id],
        [OrganizationId],
        [Date],
        [ReportData],
        [CreationDate],
        [RevisionDate]
    FROM [dbo].[RiskInsightReport]
    WHERE [OrganizationId] = @OrganizationId;
