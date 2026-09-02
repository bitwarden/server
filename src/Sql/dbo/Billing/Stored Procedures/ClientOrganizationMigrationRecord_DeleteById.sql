CREATE PROCEDURE [dbo].[ClientOrganizationMigrationRecord_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ClientOrganizationMigrationRecord]
    WHERE
        [Id] = @Id
END
