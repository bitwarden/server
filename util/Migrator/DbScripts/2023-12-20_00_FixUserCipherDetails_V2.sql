--- We fixed this view in 2023-11-29_00_FixUserCipherDetails_V2 but didn't refresh the sprocs that used it

IF OBJECT_ID('[dbo].[Cipher_Delete_V2]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_Delete_V2]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_Move_V2]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_Move_V2]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_Restore_V2]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_Restore_V2]';
END
GO

IF OBJECT_ID('[dbo].[Cipher_SoftDelete_V2]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Cipher_SoftDelete_V2]';
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByIdUserId_V2]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherDetails_ReadByIdUserId_V2]';
END
GO

IF OBJECT_ID('[dbo].[CipherDetails_ReadByUserId_V2]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CipherDetails_ReadByUserId_V2]';
END
GO
