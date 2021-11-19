CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadByOfferedToEmail]
    @OfferedToEmail NVARCHAR (256) -- Should not be null
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [OfferedToEmail] = @OfferedToEmail
END
GO
