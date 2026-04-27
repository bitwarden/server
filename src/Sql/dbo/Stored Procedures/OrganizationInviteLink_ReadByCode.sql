CREATE PROCEDURE [dbo].[OrganizationInviteLink_ReadByCode]
    @Code UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInviteLinkView]
    WHERE
        [Code] = @Code
END
