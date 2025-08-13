CREATE PROCEDURE [dbo].[ClientOrganizationMigrationRecord_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ClientOrganizationMigrationRecordView]
    WHERE
        [ProviderId] = @ProviderId
END
