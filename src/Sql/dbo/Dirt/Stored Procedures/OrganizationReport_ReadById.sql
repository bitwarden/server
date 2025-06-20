CREATE PROCEDURE [dbo].[OrganizationReport_ReadById]
    @Id UNIQUEIDENTIFIER
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
    WHERE [Id] = @Id;
