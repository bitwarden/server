-- Refresh Views

IF OBJECT_ID('[dbo].[OrganizationView]') IS NOT NULL
    BEGIN
        EXECUTE sp_refreshview N'[dbo].[OrganizationView]';
    END
GO
