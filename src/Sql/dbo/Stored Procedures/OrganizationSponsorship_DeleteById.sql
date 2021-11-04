CREATE PROCEDURE [dbo].[OrganizationSponsorship_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION OrgSponsorship_DeleteById

        DELETE
        FROM
            [dbo].[OrganizationSponsorship]
        WHERE
            [Id] = @Id

    COMMIT TRANSACTION OrgSponsorship_DeleteById
END
GO
