IF OBJECT_ID('[dbo].[OrganizationUser_Deactivate]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_Deactivate]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_Activate]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_Activate]
END
GO

IF OBJECT_ID('[dbo].[OrganizationUser_SetStatusForUsersByGuidIdArray]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersByGuidIdArray]
END
GO
