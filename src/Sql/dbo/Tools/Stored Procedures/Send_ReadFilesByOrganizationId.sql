CREATE PROCEDURE [dbo].[Send_ReadFilesByOrganizationId]
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
        AND [Type] = 1
END
