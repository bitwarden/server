-- Recreate OrganizationView so that it includes the UseScim column
IF OBJECT_ID('[dbo].[OrganizationView]') IS NOT NULL 
BEGIN
    DROP VIEW [dbo].[OrganizationView]
END 
GO

CREATE VIEW [dbo].[OrganizationView]
AS
SELECT
    *
FROM
    [dbo].[Organization]
