CREATE TABLE [dbo].[ProviderPlan] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [ProviderId]     UNIQUEIDENTIFIER NOT NULL,
    [PlanType]       TINYINT          NOT NULL,
    [SeatMinimum]    INT              NULL,
    [PurchasedSeats] INT              NULL,
    [AllocatedSeats] INT              NULL,
    CONSTRAINT [PK_ProviderPlan] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ProviderPlan_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [PK_ProviderPlanType] UNIQUE ([ProviderId], [PlanType])
);
