IF OBJECT_ID('[dbo].[Organization_Read]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Read]
END
GO

IF OBJECT_ID('[dbo].[Organization_ReadByEnabled]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_ReadByEnabled]
END
GO

CREATE PROCEDURE [dbo].[Organization_ReadByEnabled]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [Enabled] = 1
END
GO

IF OBJECT_ID('[dbo].[User_ReadByPremium]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_ReadByPremium]
END
GO

CREATE PROCEDURE [dbo].[User_ReadByPremium]
    @Premium BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [Premium] = @Premium
END
GO
