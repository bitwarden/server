CREATE PROCEDURE [dbo].[OrganizationInstallation_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @InstallationId UNIQUEIDENTIFIER,
    @CreationDate DATETIME2 (7),
    @RevisionDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationInstallation]
    SET
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
