-- Clean up chore: delete unused sprocs, including unused V2 versions

IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsAndUsersByIdUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByIdUserId]
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsAndUsersByIdUserId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByIdUserId_V2]
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsAndUsersByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByUserId]
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsAndUsersByUserId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByUserId_V2]
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByIdUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByIdUserId]
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByIdUserId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByIdUserId_V2]
END
GO
