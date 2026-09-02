IF NOT EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Collection'
      AND COLUMN_NAME = 'DefaultUserCollectionEmail'
)
    BEGIN
        ALTER TABLE [dbo].[Collection]
            ADD [DefaultUserCollectionEmail] NVARCHAR(256) NULL
    END
GO

IF NOT EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = 'dbo'
      AND TABLE_NAME = 'Collection'
      AND COLUMN_NAME = 'Type'
)
    BEGIN
        ALTER TABLE [dbo].[Collection]
            ADD [Type] TINYINT NOT NULL DEFAULT (0)
    END
GO

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
