CREATE VIEW [dbo].[UserEmailDomainView]
AS
SELECT 
    Id,
    Email,
    SUBSTRING(Email, CHARINDEX('@', Email) + 1, LEN(Email)) AS EmailDomain
FROM dbo.[User]
WHERE Email IS NOT NULL 
    AND CHARINDEX('@', Email) > 0
GO
