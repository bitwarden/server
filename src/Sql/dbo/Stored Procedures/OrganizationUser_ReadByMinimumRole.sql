CREATE PROCEDURE [dbo].[OrganizationUser_ReadByMinimumRole]
    @OrganizationId UNIQUEIDENTIFIER,
    @BaseRole TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsView]
    WHERE
        OrganizationId = @OrganizationId 
        AND [Type] <= @BaseRole
END
