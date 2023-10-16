IF TYPE_ID(N'[dbo].[OrganizationUserType]') IS NOT NULL
BEGIN
    DROP TYPE [dbo].[OrganizationUserType];
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_CreateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_CreateMany];
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_UpdateMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_UpdateMany];
END
GO
