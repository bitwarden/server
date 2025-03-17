CREATE PROCEDURE [dbo].[PhishingDomain_ReadChecksum]
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        [Checksum]
    FROM
        [dbo].[PhishingDomain]
END 