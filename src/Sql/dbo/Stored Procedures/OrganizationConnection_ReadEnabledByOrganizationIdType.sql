CREATE PROCEDURE [dbo].[OrganizationConnection_ReadEnabledByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationConnectionView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [Type] = @Type AND
        [Enabled] = 1
END