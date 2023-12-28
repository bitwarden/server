-- This sproc is unused since commit c4614bfb3da5863889cd04f3f678d76e4f3bce37
-- (PR https://github.com/bitwarden/server/pull/2953)
-- Remove instead of maintaining

IF OBJECT_ID('[dbo].[OrganizationUser_ReadWithCollectionsById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadWithCollectionsById]
END
GO
