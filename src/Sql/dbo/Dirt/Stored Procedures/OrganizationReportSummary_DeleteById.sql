CREATE PROCEDURE [dbo].[OrganizationReportSummary_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    DELETE FROM [dbo].[OrganizationReportSummary]
    WHERE [Id] = @Id;
