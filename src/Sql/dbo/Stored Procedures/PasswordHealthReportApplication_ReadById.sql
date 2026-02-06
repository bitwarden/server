CREATE PROCEDURE dbo.PasswordHealthReportApplication_ReadById
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;
    
    IF @Id IS NULL
        THROW 50000, 'Id cannot be null', 1;
        
    SELECT 
        Id,
        OrganizationId,
        Uri,
        CreationDate,
        RevisionDate
    FROM [dbo].[PasswordHealthReportApplicationView]
    WHERE Id = @Id;