IF OBJECT_ID('[dbo].[CollectionUser_ReadByOrganizationUserIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserIds];
END
GO

CREATE PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserIds]
    @OrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

SELECT
    CU.*
FROM
    [dbo].[OrganizationUser] OU
    INNER JOIN
    [dbo].[CollectionUser] CU ON OU.[AccessAll] = 0 AND CU.[OrganizationUserId] = [OU].[Id]
    INNER JOIN
    @OrganizationUserIds OUI ON OUI.[Id] = OU.[Id]
END
GO

IF OBJECT_ID('[dbo].[GroupUser_ReadByOrganizationUserIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[GroupUser_ReadByOrganizationUserIds];
END
GO

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