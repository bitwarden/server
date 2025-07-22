-- Create or alter Event_Create procedure
CREATE OR ALTER PROCEDURE [dbo].[Event_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Type INT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @InstallationId UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @PolicyId UNIQUEIDENTIFIER,
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER,
    @ProviderUserId UNIQUEIDENTIFIER,
    @ProviderOrganizationId UNIQUEIDENTIFIER = NULL,
    @ActingUserId UNIQUEIDENTIFIER,
    @DeviceType SMALLINT,
    @IpAddress VARCHAR(50),
    @Date DATETIME2(7),
    @SystemUser TINYINT = NULL,
    @DomainName VARCHAR(256),
    @SecretId UNIQUEIDENTIFIER = NULL,
    @ServiceAccountId UNIQUEIDENTIFIER = NULL,
    @ProjectId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Event]
    (
        [Id],
        [Type],
        [UserId],
        [OrganizationId],
        [InstallationId],
        [ProviderId],
        [CipherId],
        [CollectionId],
        [PolicyId],
        [GroupId],
        [OrganizationUserId],
        [ProviderUserId],
        [ProviderOrganizationId],
        [ActingUserId],
        [DeviceType],
        [IpAddress],
        [Date],
        [SystemUser],
        [DomainName],
        [SecretId],
        [ServiceAccountId],
        [ProjectId]
    )
    VALUES
    (
        @Id,
        @Type,
        @UserId,
        @OrganizationId,
        @InstallationId,
        @ProviderId,
        @CipherId,
        @CollectionId,
        @PolicyId,
        @GroupId,
        @OrganizationUserId,
        @ProviderUserId,
        @ProviderOrganizationId,
        @ActingUserId,
        @DeviceType,
        @IpAddress,
        @Date,
        @SystemUser,
        @DomainName,
        @SecretId,
        @ServiceAccountId,
        @ProjectId
    );
END
GO

-- Create or alter Event_ReadPageByProjectId procedure
CREATE OR ALTER PROCEDURE [dbo].[Event_ReadPageByProjectId]
    @ProjectId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @BeforeDate DATETIME2(7),
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

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
        e.ServiceAccountId,
        e.ProjectId
    FROM
        [dbo].[EventView] e
    WHERE
        [Date] >= @StartDate
        AND (@BeforeDate IS NOT NULL OR [Date] <= @EndDate)
        AND (@BeforeDate IS NULL OR [Date] < @BeforeDate)
        AND [ProjectId] = @ProjectId
    ORDER BY [Date] DESC
    OFFSET 0 ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- Create or alter Event_ReadPageBySecretId procedure
CREATE OR ALTER PROCEDURE [dbo].[Event_ReadPageBySecretId]
    @SecretId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @BeforeDate DATETIME2(7),
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON;

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
        e.ServiceAccountId,
        e.ProjectId
    FROM
        [dbo].[EventView] e
    WHERE
        [Date] >= @StartDate
        AND (@BeforeDate IS NOT NULL OR [Date] <= @EndDate)
        AND (@BeforeDate IS NULL OR [Date] < @BeforeDate)
        AND [SecretId] = @SecretId
    ORDER BY [Date] DESC
    OFFSET 0 ROWS
    FETCH NEXT @PageSize ROWS ONLY;
END
GO
