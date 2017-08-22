CREATE PROCEDURE [dbo].[Organization_ReadByEnabled]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [Enabled] = 1
END