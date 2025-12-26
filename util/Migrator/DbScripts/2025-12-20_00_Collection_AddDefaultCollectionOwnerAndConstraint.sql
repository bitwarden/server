-- TODO! Handle existing data in MSSQL (and EF if releasing before this). This assumes the column is populated, if it isn't then it'll create at least 1 duplicate.
-- Add DefaultCollectionOwnerId column to Collection table for Type=1 collections
-- This enables a filtered unique constraint to prevent duplicate default collections

IF NOT EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Collection'
      AND COLUMN_NAME = 'DefaultCollectionOwnerId'
)
BEGIN
    ALTER TABLE [dbo].[Collection]
        ADD [DefaultCollectionOwnerId] UNIQUEIDENTIFIER NULL
END
GO

-- Add foreign key constraint to OrganizationUser
IF NOT EXISTS (
    SELECT *
    FROM sys.foreign_keys
    WHERE name = 'FK_Collection_OrganizationUser'
      AND parent_object_id = OBJECT_ID('[dbo].[Collection]')
)
BEGIN
    ALTER TABLE [dbo].[Collection]
        ADD CONSTRAINT [FK_Collection_OrganizationUser]
        FOREIGN KEY ([DefaultCollectionOwnerId])
        REFERENCES [dbo].[OrganizationUser] ([Id])
        ON DELETE NO ACTION
END
GO

-- Create filtered unique index to prevent duplicate default collections per user
IF NOT EXISTS (
    SELECT *
    FROM sys.indexes
    WHERE name = 'IX_Collection_DefaultCollectionOwnerId_OrganizationId_Type'
      AND object_id = OBJECT_ID('[dbo].[Collection]')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Collection_DefaultCollectionOwnerId_OrganizationId_Type]
        ON [dbo].[Collection]([DefaultCollectionOwnerId], [OrganizationId], [Type])
        WHERE [Type] = 1;
END
GO

-- Refresh dependent views to include new column
IF OBJECT_ID('[dbo].[CollectionView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[CollectionView]';
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Collection_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByIds]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Collection_ReadByIds]';
END
GO

IF OBJECT_ID('[dbo].[Collection_ReadByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Collection_ReadByOrganizationId]';
END
GO

IF OBJECT_ID('[dbo].[UserCollectionDetails]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[UserCollectionDetails]';
END
GO
