CREATE PROCEDURE [dbo].[OrganizationConnection_ReadByOrganizationIdType]
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
        [Type] = @Type
END
