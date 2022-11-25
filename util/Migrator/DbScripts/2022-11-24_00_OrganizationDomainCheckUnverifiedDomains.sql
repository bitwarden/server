CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_ReadByDatepart]
    @year int,
    @month int,
    @day int,
    @hour int
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationDomain]
WHERE [VerifiedDate] IS NULL
  AND DATEPART(year, [NextRunDate]) = @year
  AND DATEPART(month, [NextRunDate]) = @month
  AND DATEPART(day, [NextRunDate]) = @day
  AND DATEPART(hour, [NextRunDate]) = @hour
END
GO