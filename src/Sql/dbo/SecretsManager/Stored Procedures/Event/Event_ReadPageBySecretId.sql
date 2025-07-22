CREATE PROCEDURE [dbo].[Event_ReadPageBySecretId]
    @SecretId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @BeforeDate DATETIME2(7),
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON

  SELECT
        e.Id,
        e.Date,
        e.Type,
        e.UserId,
        e.OrganizationId,
        e.InstallationId,
        e.ProviderId,
        e.CipherId,
        e.CollectionId,
        e.PolicyId,
        e.GroupId,
        e.OrganizationUserId,
        e.ProviderUserId,
        e.ProviderOrganizationId,
        e.DeviceType,
        e.IpAddress,
        e.ActingUserId,
        e.SystemUser,
        e.DomainName,
        e.SecretId,
        e.ProjectId,
        e.ServiceAccountId
    FROM
        [dbo].[EventView] e
    WHERE
        [Date] >= @StartDate
        AND (@BeforeDate IS NOT NULL OR [Date] <= @EndDate)
        AND (@BeforeDate IS NULL OR [Date] < @BeforeDate)
        AND [SecretId] = @SecretId
    ORDER BY [Date] DESC
    OFFSET 0 ROWS
    FETCH NEXT @PageSize ROWS ONLY
END
