CREATE PROCEDURE [dbo].[OrganizationUser_ReadManyByOrganizationIdStatus]
    @OrganizationId UNIQUEIDENTIFIER,
    @Status         TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [OrganizationId] = @OrganizationId
        AND [Status] = @Status
END
