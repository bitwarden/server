CREATE PROCEDURE [dbo].[OrganizationInviteLink_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[OrganizationInviteLink]
    WHERE
        [Id] = @Id
END
