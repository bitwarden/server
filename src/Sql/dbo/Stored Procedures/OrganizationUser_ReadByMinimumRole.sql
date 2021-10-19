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
        AND Status = 2 -- 2 = Confirmed 
        AND [Type] <= @MinRole
END
