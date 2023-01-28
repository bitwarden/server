CREATE PROCEDURE [dbo].[OrganizationConnection_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationConnectionView]
    WHERE
        [Id] = @Id
END
