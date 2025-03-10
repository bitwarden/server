-- ProviderInvoiceItem

-- Table
IF OBJECT_ID('[dbo].[ProviderInvoiceItem]') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProviderInvoiceItem] (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [ProviderId]     UNIQUEIDENTIFIER NOT NULL,
        [InvoiceId]      VARCHAR (50) NOT NULL,
        [InvoiceNumber]  VARCHAR (50) NOT NULL,
        [ClientName]     NVARCHAR (50) NOT NULL,
        [PlanName]       NVARCHAR (50) NOT NULL,
        [AssignedSeats]  INT NOT NULL,
        [UsedSeats]      INT NOT NULL,
        [Total]          MONEY NOT NULL,
        [Created]        DATETIME2 (7) NOT NULL,
        CONSTRAINT [PK_ProviderInvoiceItem] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ProviderInvoiceItem_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]),
        CONSTRAINT [PK_ProviderIdInvoiceId] UNIQUE ([ProviderId], [InvoiceId])
    );
END
GO

-- View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'ProviderInvoiceItemView')
BEGIN
    DROP VIEW [dbo].[ProviderInvoiceItemView]
END
GO

CREATE VIEW [dbo].[ProviderInvoiceItemView]
AS
SELECT
    *
FROM
    [dbo].[ProviderInvoiceItem]
GO

-- Stored Procedure: Create
IF OBJECT_ID('[dbo].[ProviderInvoiceItem_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderInvoiceItem_Create]
END
GO

CREATE PROCEDURE [dbo].[ProviderInvoiceItem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ProviderId UNIQUEIDENTIFIER,
    @InvoiceId VARCHAR (50),
    @InvoiceNumber VARCHAR (50),
    @ClientName NVARCHAR (50),
    @PlanName NVARCHAR (50),
    @AssignedSeats INT,
    @UsedSeats INT,
    @Total MONEY
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ProviderInvoiceItem]
    (
        [Id],
        [ProviderId],
        [InvoiceId],
        [InvoiceNumber],
        [ClientName],
        [PlanName],
        [AssignedSeats],
        [UsedSeats],
        [Total],
        [Created]
    )
    VALUES
    (
        @Id,
        @ProviderId,
        @InvoiceId,
        @InvoiceNumber,
        @ClientName,
        @PlanName,
        @AssignedSeats,
        @UsedSeats,
        @Total,
        GETUTCDATE()
    )
END
GO

-- Stored Procedure: DeleteById
IF OBJECT_ID('[dbo].[ProviderInvoiceItem_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderInvoiceItem_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[ProviderInvoiceItem_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ProviderInvoiceItem]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedure: ReadById
IF OBJECT_ID('[dbo].[ProviderInvoiceItem_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderInvoiceItem_ReadById]
END
GO

CREATE PROCEDURE [dbo].[ProviderInvoiceItem_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderInvoiceItemView]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedure: ReadByInvoiceId
IF OBJECT_ID('[dbo].[ProviderInvoiceItem_ReadByInvoiceId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderInvoiceItem_ReadByInvoiceId]
END
GO

CREATE PROCEDURE [dbo].[ProviderInvoiceItem_ReadByInvoiceId]
    @InvoiceId VARCHAR (50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderInvoiceItemView]
    WHERE
        [InvoiceId] = @InvoiceId
END
GO

-- Stored Procedure: ReadByProviderId
IF OBJECT_ID('[dbo].[ProviderInvoiceItem_ReadByProviderId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderInvoiceItem_ReadByProviderId]
END
GO

CREATE PROCEDURE [dbo].[ProviderInvoiceItem_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderInvoiceItemView]
    WHERE
        [ProviderId] = @ProviderId
END
GO

-- Stored Procedure: Update
IF OBJECT_ID('[dbo].[ProviderInvoiceItem_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ProviderInvoiceItem_Update]
END
GO

CREATE PROCEDURE [dbo].[ProviderInvoiceItem_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @InvoiceId VARCHAR (50),
    @InvoiceNumber VARCHAR (50),
    @ClientName NVARCHAR (50),
    @PlanName NVARCHAR (50),
    @AssignedSeats INT,
    @UsedSeats INT,
    @Total MONEY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[ProviderInvoiceItem]
    SET
        [ProviderId] = @ProviderId,
        [InvoiceId] = @InvoiceId,
        [InvoiceNumber] = @InvoiceNumber,
        [ClientName] = @ClientName,
        [PlanName] = @PlanName,
        [AssignedSeats] = @AssignedSeats,
        [UsedSeats] = @UsedSeats,
        [Total] = @Total
    WHERE
        [Id] = @Id
END
GO