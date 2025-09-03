CREATE PROCEDURE [dbo].[OrganizationInstallation_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationInstallation]
    WHERE
        [Id] = @Id
END
