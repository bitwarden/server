-- Table
IF OBJECT_ID('[dbo].[SubscriptionDiscount]') IS NULL
BEGIN
    CREATE TABLE [dbo].[SubscriptionDiscount] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [StripeCouponId] VARCHAR(50) NOT NULL,
        [StripeProductIds] NVARCHAR(MAX) NULL,
        [PercentOff] DECIMAL(5,2) NULL,
        [AmountOff] BIGINT NULL,
        [Currency] VARCHAR(10) NULL,
        [Duration] VARCHAR(20) NOT NULL,
        [DurationInMonths] INT NULL,
        [Name] NVARCHAR(100) NULL,
        [StartDate] DATETIME2(7) NOT NULL,
        [EndDate] DATETIME2(7) NOT NULL,
        [AudienceType] INT NOT NULL CONSTRAINT [DF_SubscriptionDiscount_AudienceType] DEFAULT (0),
        [CreationDate] DATETIME2(7) NOT NULL,
        [RevisionDate] DATETIME2(7) NOT NULL,
        CONSTRAINT [PK_SubscriptionDiscount] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [IX_SubscriptionDiscount_StripeCouponId] UNIQUE ([StripeCouponId])
    );
END
GO

-- Index for date range queries
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SubscriptionDiscount_DateRange' AND object_id = OBJECT_ID('[dbo].[SubscriptionDiscount]'))
BEGIN
    CREATE INDEX [IX_SubscriptionDiscount_DateRange] ON [dbo].[SubscriptionDiscount]
        ([StartDate], [EndDate]) INCLUDE ([StripeProductIds], [AudienceType]);
END
GO

-- View
CREATE OR ALTER VIEW [dbo].[SubscriptionDiscountView]
AS
SELECT
    [Id],
    [StripeCouponId],
    [StripeProductIds],
    [PercentOff],
    [AmountOff],
    [Currency],
    [Duration],
    [DurationInMonths],
    [Name],
    [StartDate],
    [EndDate],
    [AudienceType],
    [CreationDate],
    [RevisionDate]
FROM
    [dbo].[SubscriptionDiscount]
GO

-- Stored Procedures: Create
CREATE OR ALTER PROCEDURE [dbo].[SubscriptionDiscount_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @StripeCouponId VARCHAR(50),
    @StripeProductIds NVARCHAR(MAX),
    @PercentOff DECIMAL(5,2),
    @AmountOff BIGINT,
    @Currency VARCHAR(10),
    @Duration VARCHAR(20),
    @DurationInMonths INT,
    @Name NVARCHAR(100),
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @AudienceType INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SubscriptionDiscount]
    (
        [Id],
        [StripeCouponId],
        [StripeProductIds],
        [PercentOff],
        [AmountOff],
        [Currency],
        [Duration],
        [DurationInMonths],
        [Name],
        [StartDate],
        [EndDate],
        [AudienceType],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @StripeCouponId,
        @StripeProductIds,
        @PercentOff,
        @AmountOff,
        @Currency,
        @Duration,
        @DurationInMonths,
        @Name,
        @StartDate,
        @EndDate,
        @AudienceType,
        @CreationDate,
        @RevisionDate
    )
END
GO

-- Stored Procedures: DeleteById
CREATE OR ALTER PROCEDURE [dbo].[SubscriptionDiscount_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[SubscriptionDiscount]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: ReadById
CREATE OR ALTER PROCEDURE [dbo].[SubscriptionDiscount_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubscriptionDiscountView]
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: Update
CREATE OR ALTER PROCEDURE [dbo].[SubscriptionDiscount_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @StripeCouponId VARCHAR(50),
    @StripeProductIds NVARCHAR(MAX),
    @PercentOff DECIMAL(5,2),
    @AmountOff BIGINT,
    @Currency VARCHAR(10),
    @Duration VARCHAR(20),
    @DurationInMonths INT,
    @Name NVARCHAR(100),
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @AudienceType INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[SubscriptionDiscount]
    SET
        [StripeCouponId] = @StripeCouponId,
        [StripeProductIds] = @StripeProductIds,
        [PercentOff] = @PercentOff,
        [AmountOff] = @AmountOff,
        [Currency] = @Currency,
        [Duration] = @Duration,
        [DurationInMonths] = @DurationInMonths,
        [Name] = @Name,
        [StartDate] = @StartDate,
        [EndDate] = @EndDate,
        [AudienceType] = @AudienceType,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

-- Stored Procedures: ReadActive (returns discounts within date range)
CREATE OR ALTER PROCEDURE [dbo].[SubscriptionDiscount_ReadActive]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubscriptionDiscountView]
    WHERE
        [StartDate] <= GETUTCDATE()
        AND [EndDate] >= GETUTCDATE()
END
GO

-- Stored Procedures: ReadByStripeCouponId
CREATE OR ALTER PROCEDURE [dbo].[SubscriptionDiscount_ReadByStripeCouponId]
    @StripeCouponId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubscriptionDiscountView]
    WHERE
        [StripeCouponId] = @StripeCouponId
END
GO
