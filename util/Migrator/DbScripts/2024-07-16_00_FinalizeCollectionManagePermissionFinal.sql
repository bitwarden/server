-- Drop the v2 naming for sprocs that added the CollectionUser.Manage and CollectionGroup.Manage columns.
-- 2024-06-25_00_FinalizeCollectionManagePermission duplicated the v2 sprocs back to v0
-- Step 2: delete v2 sprocs

IF OBJECT_ID('[dbo].[Group_CreateWithCollections_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_CreateWithCollections_V2]
END
GO

IF OBJECT_ID('[dbo].[Group_UpdateWithCollections_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_UpdateWithCollections_V2]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_CreateWithCollections_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_CreateWithCollections_V2]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_UpdateWithCollections_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections_V2]
END
GO

IF OBJECT_ID('[dbo].[Collection_CreateWithGroupsAndUsers_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_CreateWithGroupsAndUsers_V2]
END
GO

IF OBJECT_ID('[dbo].[Collection_UpdateWithGroupsAndUsers_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_UpdateWithGroupsAndUsers_V2]
END
GO

IF OBJECT_ID('[dbo].[CollectionUser_UpdateUsers_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUser_UpdateUsers_V2]
END
GO
