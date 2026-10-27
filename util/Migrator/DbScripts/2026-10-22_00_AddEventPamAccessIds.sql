-- Adds AccessRequestId/AccessLeaseId to Event so the org log names which request or lease.
-- AccessAuditEvent stays the system of record; these columns cover only the fanned-out subset.
IF COL_LENGTH('[dbo].[Event]', 'AccessRequestId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Event]
        ADD [AccessRequestId] UNIQUEIDENTIFIER NULL;
END
GO

IF COL_LENGTH('[dbo].[Event]', 'AccessLeaseId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Event]
        ADD [AccessLeaseId] UNIQUEIDENTIFIER NULL;
END
GO

-- Refresh the view so SELECT * surfaces the new columns.
EXECUTE sp_refreshview N'[dbo].[EventView]';
GO

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
    @ProviderOrganizationId UNIQUEIDENTIFIER = null,
    @ActingUserId UNIQUEIDENTIFIER,
    @DeviceType SMALLINT,
    @IpAddress VARCHAR(50),
    @Date DATETIME2(7),
    @SystemUser TINYINT = null,
    @DomainName VARCHAR(256),
    @SecretId UNIQUEIDENTIFIER = null,
    @ServiceAccountId UNIQUEIDENTIFIER = null,
    @ProjectId UNIQUEIDENTIFIER = null,
    @GrantedServiceAccountId UNIQUEIDENTIFIER = null,
    @SendId UNIQUEIDENTIFIER = null,
    @AccessRequestId UNIQUEIDENTIFIER = null,
    @AccessLeaseId UNIQUEIDENTIFIER = null
AS
BEGIN
    SET NOCOUNT ON

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
        [ProjectId],
        [GrantedServiceAccountId],
        [SendId],
        [AccessRequestId],
        [AccessLeaseId]
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
        @ProjectId,
        @GrantedServiceAccountId,
        @SendId,
        @AccessRequestId,
        @AccessLeaseId
    )
END
GO
