CREATE PROCEDURE [dbo].[User_ReadBySsoUserOrganizationIdExternalId]
    @OrganizationId UNIQUEIDENTIFIER,
    @ExternalId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        U.*
    FROM
        [dbo].[UserView] U
    INNER JOIN
        [dbo].[SsoUser] SU ON SU.[UserId] = U.[Id]
    WHERE
        SU.[OrganizationId] = @OrganizationId
        AND SU.[ExternalId] = @ExternalId
END