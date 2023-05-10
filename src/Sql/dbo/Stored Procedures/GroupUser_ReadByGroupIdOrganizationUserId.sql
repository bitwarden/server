CREATE PROCEDURE [dbo].[GroupUser_ReadByGroupIdOrganizationUserId]
    @Id UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    SELECT
        GU.*
    FROM
        [dbo].GroupUser GU
    WHERE
        GU.GroupId = @Id
        AND GU.OrganizationUserId = @OrganizationUserId
END
