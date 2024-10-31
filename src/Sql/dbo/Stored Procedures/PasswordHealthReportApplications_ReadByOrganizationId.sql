CREATE OR ALTER PROC dbo.PasswordHealthReportApplications_ReadByOrganizationId
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
    
    IF @OrganizationId IS NULL
        THROW 50000, 'OrganizationId cannot be null', 1;
        
    SELECT 
        Id,
        OrganizationId,
        Uri,
        CreationDate,
        RevisionDate
    FROM [dbo].[PasswordHealthReportApplicationsView]
    WHERE OrganizationId = @OrganizationId;
GO