CREATE PROCEDURE [dbo].[OrganizationInviteLink_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInviteLinkView]
    WHERE
        [OrganizationId] = @OrganizationId
END
