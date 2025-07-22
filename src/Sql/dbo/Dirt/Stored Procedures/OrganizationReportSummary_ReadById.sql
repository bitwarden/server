CREATE PROCEDURE [dbo].[OrganizationReportSummary_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
    SELECT
        *
    FROM [dbo].[OrganizationReportSummary]
    WHERE [Id] = @Id;
