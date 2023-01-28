CREATE PROCEDURE [dbo].[OrganizationConnection_OrganizationDeleted]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[OrganizationConnection]
    WHERE
        [OrganizationId] = @Id
END
