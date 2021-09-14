-- TODO: copy to SQL project once code is settled

-- PolicyDetailsByUser
IF OBJECT_ID('[dbo].[PolicyDetailsByUser]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[PolicyDetailsByUser]
END
GO

CREATE FUNCTION [dbo].[PolicyDetailsByUser]
(
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus TINYINT
)
RETURNS TABLE
AS RETURN
SELECT
    P.*
FROM
    [dbo].[PolicyView] P
INNER JOIN
    [dbo].[OrganizationUserView] OU ON P.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[ProviderUserProviderOrganizationDetailsView] PUPO 
        ON OU.[OrganizationId] = PUPO.[OrganizationId]
        AND OU.[UserId] = PUPO.[UserId]
WHERE
    OU.[UserId] = @UserId 
    AND P.[Type] = @PolicyType
    AND P.[Enabled] = 1
    AND OU.[Status] >= @MinimumStatus
    AND OU.[Type] >= 2              -- Not an owner (0) or admin (1)
    AND (                           -- Can't manage policies
        OU.[Permissions] IS NULL
        OR COALESCE(JSON_VALUE(OU.[Permissions], '$.managePolicies'), 'false') = 'false'
    )
    AND PUPO.[ProviderId] IS NULL   -- Not a provider
GO

-- Policy_ReadByTypeApplicableToUser
IF OBJECT_ID('[dbo].[Policy_ReadByTypeApplicableToUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadByTypeApplicableToUser]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadByTypeApplicableToUser]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PolicyDetailsByUser](@UserId, @PolicyType, @MinimumStatus)
END

-- Policy_CountByTypeApplicableToUser
IF OBJECT_ID('[dbo].[Policy_CountByTypeApplicableToUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_CountByTypeApplicableToUser]
END
GO

CREATE PROCEDURE [dbo].[Policy_CountByTypeApplicableToUser]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus TINYINT
AS
BEGIN
    SET NOCOUNT ON

    COUNT(1)
    FROM [dbo].[PolicyDetailsByUser](@UserId, @PolicyType, @MinimumStatus)
END
