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
