-- Refresh view
IF OBJECT_ID('[dbo].[ReceiveView]') IS NOT NULL
    BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ReceiveView]';
END
GO
