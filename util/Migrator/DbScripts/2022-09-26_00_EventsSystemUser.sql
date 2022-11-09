-- Add column SystemUser to Event table
IF COL_LENGTH('[dbo].[Event]', 'SystemUser') IS NULL
    BEGIN
        ALTER TABLE
            [dbo].[Event]
        ADD
            [SystemUser] TINYINT NULL;
    END
GO
    
-- Recreate EventView so that it includes the SystemUser column
IF OBJECT_ID('[dbo].[EventView]') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[EventView]
END
GO

CREATE VIEW [dbo].[EventView]
AS
SELECT
    *
FROM
    [dbo].[Event]
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
    @SystemUser TINYINT = null
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
        [SystemUser]
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
        @SystemUser
    )
END
GO
