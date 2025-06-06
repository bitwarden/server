CREATE PROCEDURE [dbo].[ClientOrganizationMigrationRecord_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ClientOrganizationMigrationRecordView]
    WHERE
        [OrganizationId] = @OrganizationId
END
