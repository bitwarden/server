-- chore: drop v2 sprocs and functions that are no longer in use

IF OBJECT_ID('[dbo].[Collection_ReadByUserId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadByUserId_V2]
END
GO

IF OBJECT_ID('[dbo].[UserCollectionDetails_V2]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[UserCollectionDetails_V2]
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByUserId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByUserId_V2]
END
GO

IF OBJECT_ID('[dbo].[UserCipherDetails_V2]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[UserCipherDetails_V2]
END
GO
