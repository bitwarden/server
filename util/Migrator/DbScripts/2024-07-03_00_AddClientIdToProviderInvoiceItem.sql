-- Add 'ClientId' column to 'ProviderInvoiceItem' table.
IF COL_LENGTH('[dbo].[ProviderInvoiceItem]', 'ClientId') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[ProviderInvoiceItem]
    ADD
        [ClientId] UNIQUEIDENTIFIER NULL;
END
GO

-- Recreate 'ProviderInvoiceItemView' so that it includes the 'ClientId' column.
CREATE OR ALTER VIEW [dbo].[ProviderInvoiceItemView]
AS
SELECT
    *
FROM
    [dbo].[ProviderInvoiceItem]
GO

-- Alter 'ProviderInvoiceItem_Create' SPROC to add 'ClientId' column.
CREATE OR ALTER PROCEDURE [dbo].[ProviderInvoiceItem_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ProviderId UNIQUEIDENTIFIER,
    @InvoiceId VARCHAR (50),
    @InvoiceNumber VARCHAR (50),
    @ClientName NVARCHAR (50),
    @PlanName NVARCHAR (50),
    @AssignedSeats INT,
    @UsedSeats INT,
    @Total MONEY,
    @Created DATETIME2 (7) = NULL,
    @ClientId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    SET @Created = COALESCE(@Created, GETUTCDATE())

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
        [Created],
        [ClientId]
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
        @Created,
        @ClientId
    )
END
GO

-- Alter 'ProviderInvoiceItem_Update' SPROC to add 'ClientId' column.
CREATE OR ALTER PROCEDURE [dbo].[ProviderInvoiceItem_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @InvoiceId VARCHAR (50),
    @InvoiceNumber VARCHAR (50),
    @ClientName NVARCHAR (50),
    @PlanName NVARCHAR (50),
    @AssignedSeats INT,
    @UsedSeats INT,
    @Total MONEY,
    @Created DATETIME2 (7) = NULL,
    @ClientId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    SET @Created = COALESCE(@Created, GETUTCDATE())

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
        [Total] = @Total,
        [Created] = @Created,
        [ClientId] = @ClientId
    WHERE
        [Id] = @Id
END
GO

-- Refresh modules for SPROCs reliant on 'ProviderInvoiceItem' table/view.
IF OBJECT_ID('[dbo].[ProviderInvoiceItem_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ProviderInvoiceItem_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[ProviderInvoiceItem_ReadByInvoiceId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ProviderInvoiceItem_ReadByInvoiceId]';
END
GO

IF OBJECT_ID('[dbo].[ProviderInvoiceItem_ReadByProviderId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ProviderInvoiceItem_ReadByProviderId]';
END
GO
