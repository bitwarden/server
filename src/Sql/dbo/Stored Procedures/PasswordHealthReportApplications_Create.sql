CREATE OR ALTER PROC dbo.PasswordHealthReportApplications_Create
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Uri nvarchar(max),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    INSERT INTO dbo.PasswordHealthReportApplications ( Id, OrganizationId, Uri, CreationDate, RevisionDate ) 
    VALUES ( @Id, @OrganizationId, @Uri, @CreationDate, @RevisionDate )
GO
