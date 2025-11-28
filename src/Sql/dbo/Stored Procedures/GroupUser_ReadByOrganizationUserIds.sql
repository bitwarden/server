CREATE PROCEDURE [dbo].[GroupUser_ReadByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        GU.*
    FROM
        [dbo].[GroupUser] GU
    INNER JOIN
        @OrganizationUserIds OUI ON OUI.[Id] = GU.[OrganizationUserId]
END