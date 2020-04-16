CREATE PROCEDURE [dbo].[Policy_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PolicyView]
    WHERE
        [OrganizationId] = @OrganizationId
END