-- Add indexes for Organization
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Organization_GatewayCustomerId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_GatewayCustomerId]
        ON [dbo].[Organization]([GatewayCustomerId])
        WHERE [GatewayCustomerId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Organization_GatewaySubscriptionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_GatewaySubscriptionId]
        ON [dbo].[Organization]([GatewaySubscriptionId])
        WHERE [GatewaySubscriptionId] IS NOT NULL;
END
GO

-- Add indexes for Provider
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Provider_GatewayCustomerId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Provider_GatewayCustomerId]
        ON [dbo].[Provider]([GatewayCustomerId])
        WHERE [GatewayCustomerId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Provider_GatewaySubscriptionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Provider_GatewaySubscriptionId]
        ON [dbo].[Provider]([GatewaySubscriptionId])
        WHERE [GatewaySubscriptionId] IS NOT NULL;
END
GO

-- Add indexes for User
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_GatewayCustomerId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_User_GatewayCustomerId]
        ON [dbo].[User]([GatewayCustomerId])
        WHERE [GatewayCustomerId] IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_User_GatewaySubscriptionId')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_User_GatewaySubscriptionId]
        ON [dbo].[User]([GatewaySubscriptionId])
        WHERE [GatewaySubscriptionId] IS NOT NULL;
END
GO

-- Create stored procedures
CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Provider_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Provider_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ReadByGatewayCustomerId]
    @GatewayCustomerId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewayCustomerId] = @GatewayCustomerId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ReadByGatewaySubscriptionId]
    @GatewaySubscriptionId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        [GatewaySubscriptionId] = @GatewaySubscriptionId
END
GO
