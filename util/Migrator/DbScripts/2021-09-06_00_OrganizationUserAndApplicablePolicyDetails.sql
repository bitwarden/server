-- Table valued function that matches orgUsers with policies that apply to them
IF OBJECT_ID('[dbo].[OrganizationUserAndApplicablePolicyDetails]') IS NOT NULL
BEGIN
    DROP FUNCTION [dbo].[OrganizationUserAndApplicablePolicyDetails]
END
GO

CREATE FUNCTION [dbo].[OrganizationUserAndApplicablePolicyDetails]
(
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus TINYINT
)
RETURNS TABLE
AS RETURN
SELECT
    OU.[Id],
    OU.[OrganizationId],
    OU.[UserId],
    OU.[Email],
    OU.[Key],
    OU.[ResetPasswordKey],
    OU.[Status],
    OU.[Type],
    OU.[AccessAll],
    OU.[ExternalId],
    OU.[CreationDate],
    OU.[RevisionDate],
    OU.[Permissions],
    P.[Id] AS [PolicyId],
    P.[Type] AS [PolicyType],
    P.[Data] AS [PolicyData],
    P.[Enabled] AS [PolicyEnabled],
    P.[CreationDate] AS [PolicyCreationDate],
    P.[RevisionDate] AS [PolicyRevisionDate]
FROM
    [dbo].[OrganizationUserView] OU
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
GO

-- Get OrgUsers subject to a specified Policy Type
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
        [Id],
        [OrganizationId],
        [UserId],
        [Email],
        [Key],
        [ResetPasswordKey],
        [Status],
        [Type],
        [AccessAll],
        [ExternalId],
        [CreationDate],
        [RevisionDate],
        [Permissions]
    FROM [dbo].[OrganizationUserAndApplicablePolicyDetails](@UserId, @PolicyType, @MinimumStatus)
END
GO

-- Get policies of Type that apply to a specified User
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

    SELECT
        [PolicyId] AS [Id],
        [OrganizationId],
        [PolicyType] AS [Type],
        [PolicyData] AS [Data],
        [PolicyEnabled] AS [Enabled],
        [PolicyCreationDate] AS [CreationDate],
        [PolicyRevisionDate] AS [RevisionDate]
    FROM [dbo].[OrganizationUserAndApplicablePolicyDetails](@UserId, @PolicyType, @MinimumStatus)
END
