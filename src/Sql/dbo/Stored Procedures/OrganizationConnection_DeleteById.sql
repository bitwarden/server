CREATE PROCEDURE [dbo].[OrganizationConnection_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[OrganizationConnection]
    WHERE
        [Id] = @Id
END
