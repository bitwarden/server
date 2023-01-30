CREATE OR ALTER PROCEDURE [dbo].[Group_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[Group]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadOccupySeatCountByOrganizationId]
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
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByLicenseKey]
    @LicenseKey VARCHAR (100)
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationView]
WHERE
    [LicenseKey] = @LicenseKey
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadSelfHostedDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    EXEC [dbo].[Organization_ReadById] @Id
    EXEC [dbo].[OrganizationUser_ReadOccupySeatCountByOrganizationId] @Id
    EXEC [dbo].[Collection_ReadCountByOrganizationId] @Id
    EXEC [dbo].[Group_ReadCountByOrganizationId] @Id
    EXEC [dbo].[OrganizationUser_ReadByOrganizationId] @Id
    EXEC [dbo].[Policy_ReadByOrganizationId] @Id
    EXEC [dbo].[SsoConfig_ReadByOrganizationId] @Id
    EXEC [dbo].[OrganizationConnection_ReadByOrganizationIdType] @Id, 2 --Scim connection type
END
GO
