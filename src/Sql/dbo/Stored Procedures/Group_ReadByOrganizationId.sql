CREATE PROCEDURE [dbo].[Group_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GroupView]
    WHERE
        [OrganizationId] = @OrganizationId
END