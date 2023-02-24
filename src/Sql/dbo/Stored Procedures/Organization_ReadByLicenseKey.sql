CREATE PROCEDURE [dbo].[Organization_ReadByLicenseKey]
    @LicenseKey VARCHAR (100)
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationView]
WHERE
    [LicenseKey] = @LicenseKey
END
