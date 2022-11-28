CREATE PROCEDURE [dbo].[OrganizationDomain_ReadByNextRunDate]
    @Year int,
    @Month int,
    @Day int,
    @Hour int
AS
BEGIN
    SET NOCOUNT ON
        
    SELECT
        *
    FROM
        [dbo].[OrganizationDomain]
    WHERE [VerifiedDate] IS NULL
    AND [NextRunCount] != 3
    AND DATEPART(year, [NextRunDate]) = @Year
    AND DATEPART(month, [NextRunDate]) = @Month
    AND DATEPART(day, [NextRunDate]) = @Day
    AND DATEPART(hour, [NextRunDate]) = @Hour
END