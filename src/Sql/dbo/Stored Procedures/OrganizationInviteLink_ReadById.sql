CREATE PROCEDURE [dbo].[OrganizationInviteLink_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInviteLinkView]
    WHERE
        [Id] = @Id
END
