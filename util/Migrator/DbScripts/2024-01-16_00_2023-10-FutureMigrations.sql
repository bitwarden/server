-- Remove old stored procedures and SelectionReadOnlyArray for Flexible Collections
-- They have been superseded via their respective _V2 variants and the CollectionAccessSelectionType

IF OBJECT_ID('[dbo].[CollectionUser_UpdateUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionUser_UpdateUsers]
END
GO

IF OBJECT_ID('[dbo].[Group_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_UpdateWithCollections]
END
GO

IF OBJECT_ID('[dbo].[Collection_UpdateWithGroupsAndUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_UpdateWithGroupsAndUsers]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_UpdateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_UpdateWithCollections]
END
GO

IF OBJECT_ID('[dbo].[Group_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_CreateWithCollections]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_CreateWithCollections]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_CreateWithCollections]
END
GO

IF OBJECT_ID('[dbo].[Collection_CreateWithGroupsAndUsers]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_CreateWithGroupsAndUsers]
END
GO

IF TYPE_ID('[dbo].[SelectionReadOnlyArray]') IS NOT NULL
BEGIN
    DROP TYPE [dbo].[SelectionReadOnlyArray]
END
GO
