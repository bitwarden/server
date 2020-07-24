-- ID increments from 0 for backwards compatability with existing enum
CREATE TABLE [dbo].[PlanType] (
	[Id]                            [INT]               IDENTITY(0,1) NOT NULL,
	[StripePlanId]                  [NVARCHAR](50)      NULL,
	[StripeSeatPlanId]              [NVARCHAR](50)      NULL,
	[StripeStoragePlanId]           [NVARCHAR](50)      NULL,
	[StripePremiumAccessPlanId]     [NVARCHAR](50)      NULL,
	[BasePrice]                     [DECIMAL](18, 2)    NULL,
	[SeatPrice]                     [DECIMAL](18, 2)    NULL,
	[AdditionalStoragePricePerGb]   [DECIMAL](18, 2)    NULL,
	[HasPremiumAccessAddonCost]     [DECIMAL](18, 2)    NULL,
	[IsAnnual]                      [BIT]               NOT NULL,
	[PlanTypeGroupId]               [INT]               NOT NULL,
    CONSTRAINT [PK_PlanType] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_PlanType_PlanTypeGrouo] FOREIGN KEY ([PlanTypeGroupId]) REFERENCES [dbo].[PlanTypeGroup] ([Id])
);
