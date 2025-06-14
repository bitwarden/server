CREATE PROCEDURE [dbo].[OrganizationReport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

IF @Id IS NULL
        THROW 50000, 'Id cannot be null', 1;

SELECT
    [Id],
    [OrganizationId],
    [Date],
    [ReportData],
    [CreationDate],
    [RevisionDate]
FROM [dbo].[OrganizationReport]
WHERE [Id] = @Id;
