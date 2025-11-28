-- Drop existing SPROC
IF OBJECT_ID('[dbo].[Organization_ReadAddableToProviderByUserId') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_ReadAddableToProviderByUserId]
END
GO

CREATE PROCEDURE [dbo].[Organization_ReadAddableToProviderByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ProviderType TINYINT
AS
BEGIN
    SET NOCOUNT ON
    SELECT O.* FROM [dbo].[OrganizationUser] AS OU
    JOIN [dbo].[Organization] AS O ON O.[Id] = OU.[OrganizationId]
    WHERE
        OU.[UserId] = @UserId AND
        OU.[Type] = 0 AND
        OU.[Status] = 2 AND
        O.[Enabled] = 1 AND
        O.[GatewayCustomerId] IS NOT NULL AND
        O.[GatewaySubscriptionId] IS NOT NULL AND
        O.[Seats] > 0 AND
        O.[Status] = 1 AND
        O.[UseSecretsManager] = 0 AND
      -- All Teams & Enterprise for MSP
        (@ProviderType = 0 AND O.[PlanType] IN (2, 3, 4, 5, 8, 9, 10, 11, 12, 13, 14, 15, 17, 18, 19, 20) OR
            -- All Enterprise for MOE
         @ProviderType = 2 AND O.[PlanType] IN (4, 5, 10, 11, 14, 15, 19, 20));
END
GO
