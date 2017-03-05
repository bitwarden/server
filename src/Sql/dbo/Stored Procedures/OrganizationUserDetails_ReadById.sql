CREATE PROCEDURE [dbo].[OrganizationUserDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserDetailsView]
    WHERE
        [Id] = @Id
END