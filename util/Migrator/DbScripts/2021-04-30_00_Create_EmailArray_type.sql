-- Create EmailArray type
IF NOT EXISTS (
    SELECT *
FROM sys.types
WHERE [Name] = 'EmailArray'
    AND is_user_defined = 1
)
CREATE TYPE [dbo].[EmailArray] AS TABLE (
    [Email] NVARCHAR(256) NOT NULL);
GO
