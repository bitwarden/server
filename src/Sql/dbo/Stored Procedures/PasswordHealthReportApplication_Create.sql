CREATE PROCEDURE dbo.PasswordHealthReportApplication_Create
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Uri nvarchar(max),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    INSERT INTO dbo.PasswordHealthReportApplication ( Id, OrganizationId, Uri, CreationDate, RevisionDate ) 
    VALUES ( @Id, @OrganizationId, @Uri, @CreationDate, @RevisionDate )