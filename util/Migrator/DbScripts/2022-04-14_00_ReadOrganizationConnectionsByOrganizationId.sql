IF OBJECT_ID('[dbo].[OrganizationConnection_ReadEnabledByOrganizationIdType]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_ReadEnabledByOrganizationIdType];
END
GO

IF OBJECT_ID('[dbo].[OrganizationConnection_ReadByOrganizationIdType]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationConnection_ReadByOrganizationIdType];
END
GO

CREATE PROCEDURE [dbo].[OrganizationConnection_ReadByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationConnectionView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [Type] = @Type
END
GO
