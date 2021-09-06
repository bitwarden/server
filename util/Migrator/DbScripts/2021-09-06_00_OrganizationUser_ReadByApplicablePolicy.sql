IF OBJECT_ID('[dbo].[OrganizationUser_ReadByApplicablePolicyType]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_ReadByApplicablePolicyType]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_ReadByApplicablePolicyType]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        OU.*
    FROM
        [dbo].[OrganizationUserUserDetailsView] OU
    INNER JOIN
        [dbo].[PolicyView] P ON OU.[OrganizationId] = P.[OrganizationId]
    LEFT JOIN
        [dbo].[ProviderUserProviderOrganizationDetailsView] PUPO 
            ON OU.[OrganizationId] = PUPO.[OrganizationId]
            AND OU.[UserId] = PUPO.[UserId]
    WHERE
        OU.[UserId] = @UserId 
        AND P.[Type] = @PolicyType
        AND P.[Enabled] = 1
        AND OU.[Status] >= @MinimumStatus
        AND OU.[Type] >= 2              -- Not an admin or owner
        AND (                           -- Can't manage policies
            OU.[Permissions] IS NULL
            OR COALESCE(JSON_VALUE(OU.[Permissions], '$.managePolicies'), 'false') = 'false'
        )
        AND PUPO.[ProviderId] IS NULL   -- Not a provider
        -- TODO: optional check for Policy.Data JSON value
END
