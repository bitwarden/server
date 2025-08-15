CREATE PROCEDURE [dbo].[OrganizationReport_GetReportDataById]
    @OrganizationId UNIQUEIDENTIFIER,
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [ReportData]
    FROM [dbo].[OrganizationReport]
    WHERE [OrganizationId] = @OrganizationId
    AND [Id] = @Id
END

