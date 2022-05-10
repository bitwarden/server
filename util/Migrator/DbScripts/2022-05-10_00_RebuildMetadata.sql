IF EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationApiKey_ApiKey')
BEGIN
    DROP INDEX [IX_OrganizationApiKey_ApiKey] ON [dbo].[OrganizationApiKey]
END
GO

IF OBJECT_ID('[dbo].[OrganizationView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationView]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationSponsorshipView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationSponsorshipView]';
END
GO

IF OBJECT_ID('[dbo].[EventView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[EventView]';
END
GO
