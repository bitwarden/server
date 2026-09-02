IF OBJECT_ID('[dbo].[OrganizationUser_SetStatusForUsersById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_SetStatusForUsersById]
END
GO

IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByOrganizationUserIdsJson]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationUserIdsJson]
END
GO
