CREATE VIEW [dbo].[SsoConfigView]
AS
SELECT
    SSO.*
FROM
    [dbo].[SsoConfig] SSO
INNER JOIN
    [dbo].[Organization] O ON O.[Identifier] = SSO.[Identifier]
