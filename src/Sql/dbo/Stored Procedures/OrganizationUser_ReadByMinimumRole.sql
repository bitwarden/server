CREATE PROCEDURE [dbo].[OrganizationUser_ReadByMinimumRole]
    @OrganizationId UNIQUEIDENTIFIER,
    @MinRole TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsView]
    WHERE
        OrganizationId = @OrganizationId 
        AND [Type] <= @MinRole
END
