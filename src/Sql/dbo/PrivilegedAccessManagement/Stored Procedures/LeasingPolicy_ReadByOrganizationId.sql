CREATE PROCEDURE [dbo].[LeasingPolicy_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[LeasingPolicy]
    WHERE [OrganizationId] = @OrganizationId
END
