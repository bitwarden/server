CREATE OR ALTER  PROCEDURE [dbo].[OrganizationUser_ReadOccupiedSmSeatCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    SELECT
        COUNT(1)
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        OrganizationId = @OrganizationId
        AND Status >= 0 --Invited
        AND AccessSecretsManager = 1
END
GO

CREATE OR ALTER PROCEDURE [dbo].[ServiceAccount_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[ServiceAccount]
    WHERE
        OrganizationId = @OrganizationId
END
GO

