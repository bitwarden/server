CREATE PROCEDURE [dbo].[ClientOrganizationMigrationRecord_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ClientOrganizationMigrationRecordView]
    WHERE
        [Id] = @Id
END
