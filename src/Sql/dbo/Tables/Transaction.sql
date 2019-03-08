CREATE TABLE [dbo].[Transaction] (
    [Id]                    UNIQUEIDENTIFIER    NOT NULL,
    [UserId]                UNIQUEIDENTIFIER    NULL,
    [OrganizationId]        UNIQUEIDENTIFIER    NULL,
    [Type]                  TINYINT             NOT NULL,
    [Amount]                MONEY               NOT NULL,
    [Refunded]              BIT                 NULL,
    [RefundedAmount]        MONEY               NULL,
    [Details]               NVARCHAR(100)       NULL,
    [PaymentMethodType]     TINYINT             NULL,
    [Gateway]               TINYINT             NULL,
    [GatewayId]             VARCHAR(50)         NULL,
    [CreationDate]          DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_Transaction] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Transaction_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Transaction_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [IX_Transaction_Gateway_GatewayId]
    ON [dbo].[Transaction]([Gateway] ASC, [GatewayId] ASC)
    WHERE [Gateway] IS NOT NULL AND [GatewayId] IS NOT NULL;


GO
CREATE NONCLUSTERED INDEX [IX_Transaction_UserId_OrganizationId_CreationDate]
    ON [dbo].[Transaction]([UserId] ASC, [OrganizationId] ASC, [CreationDate] ASC);

