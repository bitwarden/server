IF OBJECT_ID('[dbo].[Cipher_Delete_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Delete_V2]
END
GO

IF OBJECT_ID('[dbo].[Cipher_Move_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Move_V2]
END
GO

IF OBJECT_ID('[dbo].[Cipher_Restore_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_Restore_V2]
END
GO

IF OBJECT_ID('[dbo].[Cipher_SoftDelete_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Cipher_SoftDelete_V2]
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByIdUserId_V2]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CipherDetails_ReadByIdUserId_V2]
END
GO
