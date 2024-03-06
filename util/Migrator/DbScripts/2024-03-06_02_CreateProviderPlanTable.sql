-- Table
IF OBJECT_ID('[dbo].[ProviderPlan]') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProviderPlan] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [ProviderId]     UNIQUEIDENTIFIER NOT NULL,
        [PlanType]       TINYINT          NOT NULL,
        [SeatMinimum]    INT              NULL,
        [PurchasedSeats] INT              NULL,
        [AllocatedSeats] INT              NULL,
        CONSTRAINT [PK_ProviderPlan] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ProviderPlan_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE
    );
END
GO

-- View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'ProviderPlanView')
BEGIN
    DROP VIEW [dbo].[ProviderPlanView]
END
GO

CREATE VIEW [dbo].[ProviderPlanView]
AS
SELECT
    *
FROM
    [dbo].[ProviderPlan]
GO

-- Create SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_Create]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @SeatMinimum INT,
    @PurchasedSeats INT,
    @AllocatedSeats INT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ProviderPlan]
    (
        [Id],
        [ProviderId],
        [PlanType],
        [SeatMinimum],
        [PurchasedSeats],
        [AllocatedSeats]
    )
    VALUES
    (
        @Id,
        @ProviderId,
        @PlanType,
        @SeatMinimum,
        @PurchasedSeats,
        @AllocatedSeats
    )
END
GO

-- DeleteById SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ProviderPlan]
    WHERE
        [Id] = @Id
END
GO

-- ReadById SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_ReadById]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderPlanView]
    WHERE
        [Id] = @Id
END
GO

-- ReadByProviderId SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_ReadByProviderId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_ReadByProviderId]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderPlanView]
    WHERE
        [ProviderId] = @ProviderId
END
GO

-- Update SPROC
IF OBJECT_ID('[dbo].[ProviderPlan_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderPlan_Update]
END
GO

CREATE PROCEDURE [dbo].[ProviderPlan_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @PlanType TINYINT,
    @SeatMinimum INT,
    @PurchasedSeats INT,
    @AllocatedSeats INT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ProviderPlan]
    SET
        [ProviderId] = @ProviderId,
        [PlanType] = @PlanType,
        [SeatMinimum] = @SeatMinimum,
        [PurchasedSeats] = @PurchasedSeats,
        [AllocatedSeats] = @AllocatedSeats
    WHERE
        [Id] = @Id
END
GO
