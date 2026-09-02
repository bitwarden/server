CREATE PROCEDURE [dbo].[OrganizationUser_ReadManyDetailsByRole]
    @OrganizationId UNIQUEIDENTIFIER,
    @Role TINYINT
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
        AND [Type] = @Role
END
