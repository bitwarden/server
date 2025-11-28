IF OBJECT_ID('[dbo].[OrganizationView]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationView]';
END
GO
