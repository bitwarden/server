IF OBJECT_ID('[dbo].[OrganizationUser_RevokeMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_RevokeMany]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_RestoreMany]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_RestoreMany]
END
GO
