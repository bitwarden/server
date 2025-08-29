CREATE PROCEDURE [dbo].[OrganizationReport_GetApplicationDataById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [OrganizationId],
        [ApplicationData]
    FROM [dbo].[OrganizationReportView]
    WHERE [Id] = @Id
END

