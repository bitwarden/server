CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [Id] = @Id
END
GO
