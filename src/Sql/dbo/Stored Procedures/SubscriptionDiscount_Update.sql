CREATE PROCEDURE [dbo].[SubscriptionDiscount_Update]
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
