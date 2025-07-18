CREATE TABLE [dbo].[OrganizationSubscriptionUpdate] (
    [Id] UniqueIdentifier NOT NULL,
    [OrganizationId] UniqueIdentifier NOT NULL,
    [SeatsLastUpdated] DATETIME2 NULL,
    [SyncAttempts] INT NOT NULL DEFAULT(0),
    CONSTRAINT [PK_OrganizationSubscriptionUpdate] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationSubscriptionUpdate_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
)
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationSubscriptionUpdate_SeatsLastUpdated]
    ON [dbo].[OrganizationSubscriptionUpdate]([SeatsLastUpdated] ASC)
        INCLUDE ([OrganizationId]);
GO
