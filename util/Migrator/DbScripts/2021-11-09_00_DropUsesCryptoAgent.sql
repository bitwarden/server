IF COL_LENGTH('[dbo].[User]', 'UsesCryptoAgent') IS NOT NULL
    BEGIN
        ALTER TABLE
            [dbo].[User]
        DROP COLUMN
            [UsesCryptoAgent]
    END
GO

IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'UserView')
    BEGIN
        DROP VIEW [dbo].[UserView]
    END
GO

CREATE VIEW [dbo].[UserView]
AS
SELECT
    *
FROM
    [dbo].[User]
GO