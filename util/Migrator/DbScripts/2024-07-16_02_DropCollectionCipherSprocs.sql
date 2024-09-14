-- Clean up chore: delete v2 CollectionCipher sprocs
-- These were already copied back to v0 in 2024-07-09_00_CollectionCipherRemoveAccessAll

IF OBJECT_ID('[dbo].[CollectionCipher_ReadByUserId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_ReadByUserId_V2]
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_ReadByUserIdCipherId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_ReadByUserIdCipherId_V2]
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_UpdateCollections_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_UpdateCollections_V2]
END
GO

IF OBJECT_ID('[dbo].[CollectionCipher_UpdateCollectionsForCiphers_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionCipher_UpdateCollectionsForCiphers_V2]
END
GO
