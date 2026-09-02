CREATE PROCEDURE [dbo].[SubscriptionDiscount_Create]
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
