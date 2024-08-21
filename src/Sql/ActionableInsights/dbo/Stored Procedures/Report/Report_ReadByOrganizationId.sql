CREATE PROCEDURE [dbo].[Report_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[Report]
    WHERE
        [OrganizationId] = @OrganizationId
END
