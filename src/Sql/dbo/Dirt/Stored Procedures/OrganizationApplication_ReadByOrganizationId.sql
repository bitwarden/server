CREATE PROCEDURE [dbo].[OrganizationApplication_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    SELECT
        *
    FROM [dbo].[OrganizationApplicationView]
    WHERE [OrganizationId] = @OrganizationId;
