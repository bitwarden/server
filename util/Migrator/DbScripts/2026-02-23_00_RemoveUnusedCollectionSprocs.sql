IF OBJECT_ID('[dbo].[CollectionUser_ReadByOrganizationUserIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserIds]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUserUserDetails_ReadWithCollectionsById]
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByOrganizationIdWithPermissions]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByOrganizationIdWithPermissions]
END
GO
