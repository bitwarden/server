-- This index was incorrect business logic and should be removed.
IF OBJECT_ID('[dbo].[PK_ProviderIdInvoiceId]', 'UQ') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[ProviderInvoiceItem]
        DROP CONSTRAINT [PK_ProviderIdInvoiceId]
    END
GO

-- This foreign key needs a cascade to ensure providers can be deleted when ProviderInvoiceItems still exist.
IF OBJECT_ID('[dbo].[FK_ProviderInvoiceItem_Provider]', 'F') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[ProviderInvoiceItem]
        DROP CONSTRAINT [FK_ProviderInvoiceItem_Provider]
    END
GO

ALTER TABLE [dbo].[ProviderInvoiceItem]
    ADD CONSTRAINT [FK_ProviderInvoiceItem_Provider]
    FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE
GO

-- Because we need to insert this when a "draft" invoice is created, the [InvoiceNumber] column needs to be nullable.
ALTER TABLE [dbo].[ProviderInvoiceItem]
    ALTER COLUMN [InvoiceNumber] VARCHAR (50) NULL
GO

-- The "Create" stored procedure needs to take the @Created parameter.
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
    @Total MONEY,
    @Created DATETIME2 (7)
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
        @Created
    )
END
GO