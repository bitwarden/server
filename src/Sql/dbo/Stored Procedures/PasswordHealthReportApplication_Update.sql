CREATE PROC dbo.PasswordHealthReportApplication_Update
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Uri nvarchar(max),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
    SET NOCOUNT ON;
    UPDATE dbo.PasswordHealthReportApplication 
        SET OrganizationId = @OrganizationId, 
            Uri = @Uri, 
            RevisionDate = @RevisionDate
    WHERE Id = @Id