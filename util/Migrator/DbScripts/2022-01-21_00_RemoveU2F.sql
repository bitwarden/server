IF OBJECT_ID('[dbo].[U2f_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_Create]
END
GO

IF OBJECT_ID('[dbo].[U2f_DeleteByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_DeleteByUserId]
END
GO

IF OBJECT_ID('[dbo].[U2f_DeleteOld]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_DeleteOld]
END
GO

IF OBJECT_ID('[dbo].[U2f_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_ReadByUserId]
END
GO

IF OBJECT_ID('[dbo].[U2f_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[U2f_ReadById]
END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'U2fView')
BEGIN
    DROP VIEW [dbo].[U2fView];
END
GO

IF OBJECT_ID('[dbo].[U2f]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[U2f]
END
GO