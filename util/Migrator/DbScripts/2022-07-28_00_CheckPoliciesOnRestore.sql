-- 2022-06-08_00_DeactivatedUserStatus changed UserStatus from TINYINT to SMALLINT but these sprocs were missed

-- Policy_ReadByTypeApplicableToUser
IF OBJECT_ID('[dbo].[Policy_ReadByTypeApplicableToUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_ReadByTypeApplicableToUser]
END
GO

CREATE PROCEDURE [dbo].[Policy_ReadByTypeApplicableToUser]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus SMALLINT
AS
BEGIN
    SET NOCOUNT ON

SELECT *
FROM [dbo].[PolicyApplicableToUser](@UserId, @PolicyType, @MinimumStatus)
END
GO

-- Policy_CountByTypeApplicableToUser
IF OBJECT_ID('[dbo].[Policy_CountByTypeApplicableToUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_CountByTypeApplicableToUser]
END
GO

CREATE PROCEDURE [dbo].[Policy_CountByTypeApplicableToUser]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus SMALLINT
AS
BEGIN
    SET NOCOUNT ON

SELECT COUNT(1)
FROM [dbo].[PolicyApplicableToUser](@UserId, @PolicyType, @MinimumStatus)
END
GO

-- We need to update this function because we now have OrganizationUserStatusTypes that are less than zero,
-- and Deactivated/Revoked users may or may not be associated with a UserId
IF OBJECT_ID('[dbo].[PolicyApplicableToUser]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[PolicyApplicableToUser]
END
GO

CREATE FUNCTION [dbo].[PolicyApplicableToUser]
(
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus SMALLINT
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
    (SELECT
        PU.UserId,
        PO.OrganizationId
    FROM
        [dbo].[ProviderUserView] PU
    INNER JOIN
        [ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]) PUPO
    ON PUPO.UserId = OU.UserId
    AND PUPO.OrganizationId = P.OrganizationId
WHERE
    (
        (
            OU.[Status] != 0     -- OrgUsers who have accepted their invite and are linked to a UserId
            AND OU.[UserId] = @UserId 
        )
        OR (
            OU.[Status] = 0     -- 'Invited' OrgUsers are not linked to a UserId yet, so we have to look up their email
            AND OU.[Email] IN (SELECT U.Email FROM [dbo].[UserView] U WHERE U.Id = @UserId)
    )
    )
  AND P.[Type] = @PolicyType
  AND P.[Enabled] = 1
  AND OU.[Status] >= @MinimumStatus
  AND OU.[Type] >= 2              -- Not an owner (0) or admin (1)
  AND (                           -- Can't manage policies
    OU.[Permissions] IS NULL
    OR COALESCE(JSON_VALUE(OU.[Permissions], '$.managePolicies'), 'false') = 'false'
    )
  AND PUPO.[UserId] IS NULL   -- Not a provider
GO