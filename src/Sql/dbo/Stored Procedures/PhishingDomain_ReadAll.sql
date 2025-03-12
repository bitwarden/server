CREATE PROCEDURE [dbo].[PhishingDomain_ReadAll]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Domain]
    FROM
        [dbo].[PhishingDomain]
    ORDER BY
        [Domain] ASC
END 