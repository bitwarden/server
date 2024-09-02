-- Finalise removal of OrganizationUser.AccessAll column
-- Add default column value
-- Sprocs already have default value for rollback purposes, this just supports dropping the column itself

IF OBJECT_ID('[dbo].[DF_OrganizationUser_AccessAll]', 'D') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationUser]
    ADD CONSTRAINT [DF_OrganizationUser_AccessAll] DEFAULT (0) FOR [AccessAll];
END
GO
