CREATE PROCEDURE [dbo].[OrganizationUser_ReadManyByOrganizationIdEmails]
    @OrganizationId UNIQUEIDENTIFIER,
    @Emails [dbo].[EmailArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.*
    FROM
        [dbo].[OrganizationUserView] OU
    WHERE
        OU.[OrganizationId] = @OrganizationId
        AND EXISTS (
            SELECT 1
            FROM @Emails E
            WHERE E.[Email] = OU.[Email]
        )
END
