CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_ReadByNextRunDate]
    @Date DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationDomain]
WHERE [VerifiedDate] IS NULL
  AND [JobRunCount] != 3
  AND DATEPART(year, [NextRunDate]) = DATEPART(year, @Date)
  AND DATEPART(month, [NextRunDate]) = DATEPART(month, @Date)
  AND DATEPART(day, [NextRunDate]) = DATEPART(day, @Date)
  AND DATEPART(hour, [NextRunDate]) = DATEPART(hour, @Date)
UNION
SELECT
    *
FROM
    [dbo].[OrganizationDomain]
WHERE DATEDIFF(hour, [NextRunDate], @Date) > 36
  AND [VerifiedDate] IS NULL
  AND [JobRunCount] != 3
END
GO