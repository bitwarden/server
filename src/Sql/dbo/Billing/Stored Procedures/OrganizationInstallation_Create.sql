CREATE PROCEDURE [dbo].[OrganizationInstallation_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @InstallationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationInstallation]
    (
        [Id],
        [OrganizationId],
        [InstallationId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
     @Id,
     @OrganizationId,
     @InstallationId,
     @CreationDate,
     @RevisionDate
    )
END
