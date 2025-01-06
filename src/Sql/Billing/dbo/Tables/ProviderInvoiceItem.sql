CREATE TABLE [dbo].[ProviderInvoiceItem] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [ProviderId]     UNIQUEIDENTIFIER NOT NULL,
    [InvoiceId]      VARCHAR (50) NOT NULL,
    [InvoiceNumber]  VARCHAR (50) NULL,
    [ClientName]     NVARCHAR (50) NOT NULL,
    [PlanName]       NVARCHAR (50) NOT NULL,
    [AssignedSeats]  INT NOT NULL,
    [UsedSeats]      INT NOT NULL,
    [Total]          MONEY NOT NULL,
    [Created]        DATETIME2 (7) NOT NULL,
    [ClientId]       UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_ProviderInvoiceItem] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ProviderInvoiceItem_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE
);
