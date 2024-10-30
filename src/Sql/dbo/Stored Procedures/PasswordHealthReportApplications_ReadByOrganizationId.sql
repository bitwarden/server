CREATE OR ALTER PROC dbo.PasswordHealthReportApplications_ReadByOrganizationId
    @OrganizationId UNIQUEIDENTIFIER
AS
    SELECT * FROM [dbo].[PasswordHealthReportApplicationsView]
    WHERE OrganizationId = @OrganizationId
GO