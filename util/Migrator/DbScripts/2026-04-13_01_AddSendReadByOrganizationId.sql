CREATE OR ALTER PROCEDURE [dbo].[Send_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
