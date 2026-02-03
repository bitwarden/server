CREATE TABLE [dbo].[SubscriptionDiscount] (
    [Id]                    UNIQUEIDENTIFIER NOT NULL,
    [StripeCouponId]        VARCHAR (50)     NOT NULL,
    [StripeProductIds]      NVARCHAR (MAX)   NULL,
    [PercentOff]            DECIMAL (5, 2)   NULL,
    [AmountOff]             BIGINT           NULL,
    [Currency]              VARCHAR (10)     NULL,
    [Duration]              VARCHAR (20)     NOT NULL,
    [DurationInMonths]      INT              NULL,
    [Name]                  NVARCHAR (100)   NULL,
    [StartDate]             DATETIME2 (7)    NOT NULL,
    [EndDate]               DATETIME2 (7)    NOT NULL,
    [AudienceType]          INT              NOT NULL CONSTRAINT [DF_SubscriptionDiscount_AudienceType] DEFAULT (0),
    [CreationDate]          DATETIME2 (7)    NOT NULL,
    [RevisionDate]          DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_SubscriptionDiscount] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [IX_SubscriptionDiscount_StripeCouponId] UNIQUE NONCLUSTERED ([StripeCouponId] ASC)
);

GO
CREATE NONCLUSTERED INDEX [IX_SubscriptionDiscount_DateRange]
    ON [dbo].[SubscriptionDiscount]([StartDate] ASC, [EndDate] ASC)
    INCLUDE([StripeProductIds], [AudienceType]);
