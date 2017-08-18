USE [master]
IF ((SELECT COUNT(1) FROM sys.databases WHERE [name] = 'vault') = 0)
BEGIN
    CREATE DATABASE [vault]
END
GO
