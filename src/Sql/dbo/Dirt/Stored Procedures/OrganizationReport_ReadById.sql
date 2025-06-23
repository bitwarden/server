CREATE PROCEDURE [dbo].[OrganizationReport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id;
