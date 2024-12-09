-- Table
IF OBJECT_ID('[dbo].[ClientOrganizationMigrationRecord]') IS NULL
BEGIN
    CREATE TABLE [dbo].[ClientOrganizationMigrationRecord] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [ProviderId] UNIQUEIDENTIFIER NOT NULL,
        [PlanType] TINYINT NOT NULL,
        [Seats] SMALLINT NOT NULL,
        [MaxStorageGb] SMALLINT NULL,
        [GatewayCustomerId] VARCHAR(50) NOT NULL,
        [GatewaySubscriptionId] VARCHAR(50) NOT NULL,
        [ExpirationDate] DATETIME2(7) NULL,
        [MaxAutoscaleSeats] INT NULL,
        [Status] TINYINT NOT NULL,
        CONSTRAINT [PK_ClientOrganizationMigrationRecord] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [PK_OrganizationIdProviderId] UNIQUE ([ProviderId], [OrganizationId])
    );
END
GO

-- View
CREATE OR AlTER VIEW [dbo].[ClientOrganizationMigrationRecordView]
AS
SELECT
    *
FROM
    [dbo].[ClientOrganizationMigrationRecord]
GO

-- Stored Procedures: Create
CREATE OR ALTER PROCEDURE [dbo].[ClientOrganizationMigrationRecord_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxStorageGb SMALLINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ExpirationDate DATETIME2(7),
    @MaxAutoscaleSeats INT,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ClientOrganizationMigrationRecord]
    (
        [Id],
        [OrganizationId],
        [ProviderId],
        [PlanType],
        [Seats],
        [MaxStorageGb],
        [GatewayCustomerId],
        [GatewaySubscriptionId],
        [ExpirationDate],
        [MaxAutoscaleSeats],
        [Status]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @ProviderId,
        @PlanType,
        @Seats,
        @MaxStorageGb,
        @GatewayCustomerId,
        @GatewaySubscriptionId,
        @ExpirationDate,
        @MaxAutoscaleSeats,
        @Status
    )
END
GO

-- Stored Procedures: DeleteById
CREATE OR ALTER PROCEDURE [dbo].[ClientOrganizationMigrationRecord_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ClientOrganizationMigrationRecord]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: ReadById
CREATE OR ALTER PROCEDURE [dbo].[ClientOrganizationMigrationRecord_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ClientOrganizationMigrationRecordView]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: ReadByOrganizationId
CREATE OR ALTER PROCEDURE [dbo].[ClientOrganizationMigrationRecord_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ClientOrganizationMigrationRecordView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO

-- Stored Procedures: ReadByProviderId
CREATE OR ALTER PROCEDURE [dbo].[ClientOrganizationMigrationRecord_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ClientOrganizationMigrationRecordView]
    WHERE
        [ProviderId] = @ProviderId
END
GO

-- Stored Procedures: Update
CREATE OR ALTER PROCEDURE [dbo].[ClientOrganizationMigrationRecord_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxStorageGb SMALLINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ExpirationDate DATETIME2(7),
    @MaxAutoscaleSeats INT,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ClientOrganizationMigrationRecord]
    SET
        [OrganizationId] = @OrganizationId,
        [ProviderId] = @ProviderId,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxStorageGb] = @MaxStorageGb,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [ExpirationDate] = @ExpirationDate,
        [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
        [Status] = @Status
    WHERE
        [Id] = @Id
END
GO
