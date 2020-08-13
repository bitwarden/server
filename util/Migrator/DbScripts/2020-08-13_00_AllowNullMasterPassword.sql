ALTER TABLE
    [dbo].[User]
ALTER COLUMN
    [MasterPassword] NVARCHAR (300) NULL
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
