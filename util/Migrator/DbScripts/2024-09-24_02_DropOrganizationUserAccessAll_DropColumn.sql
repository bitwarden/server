-- Finalise removal of OrganizationUser.AccessAll column
-- Drop the column

/****************************************************************
 *
 * WARNING: Index Rebuild on OrganizationUser Table!
 * Ensure [IX_OrganizationUser_UserIdOrganizationIdStatus] impact is done after-hours
 *      or scale DB instance up to handle increased load during update.
 *
 ***************************************************************/

-- Create the new index (without the column) before we drop the old index
PRINT N'Creating index IX_OrganizationUser_UserIdOrganizationIdStatusV2...';
CREATE NONCLUSTERED INDEX [IX_OrganizationUser_UserIdOrganizationIdStatusV2]
    ON [dbo].[OrganizationUser]([UserId] ASC, [OrganizationId] ASC, [Status] ASC);

-- Drop the old index that refers to the column
PRINT N'Dropping index IX_OrganizationUser_UserIdOrganizationIdStatus...';
DROP INDEX IF EXISTS [IX_OrganizationUser_UserIdOrganizationIdStatus]
    ON [dbo].[OrganizationUser];

-- Drop default constraint
IF OBJECT_ID('[dbo].[DF_OrganizationUser_AccessAll]', 'D') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationUser]
    DROP CONSTRAINT [DF_OrganizationUser_AccessAll];
END
GO

-- Drop the column
IF COL_LENGTH('[dbo].[OrganizationUser]', 'AccessAll') IS NOT NULL
BEGIN
    ALTER TABLE
        [dbo].[OrganizationUser]
    DROP COLUMN
        [AccessAll]
END
GO

-- Refresh views
IF OBJECT_ID('[dbo].[OrganizationUserView]') IS NOT NULL
    BEGIN
        EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserView]';
    END
GO
