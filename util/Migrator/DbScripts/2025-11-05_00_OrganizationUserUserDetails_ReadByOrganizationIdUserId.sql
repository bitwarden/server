CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserUserDetails_ReadByOrganizationIdUserId]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationUserUserDetailsView]
WHERE
    [OrganizationId] = @OrganizationId
  AND
    [UserId] = @UserId
END
go
