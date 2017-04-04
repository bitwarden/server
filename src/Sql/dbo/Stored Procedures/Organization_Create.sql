CREATE PROCEDURE [dbo].[Organization_Create]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(20),
    @PlanType TINYINT,
    @PlanBasePrice MONEY,
    @PlanUserPrice MONEY,
    @PlanRenewalDate DATETIME2(7),
    @PlanTrial BIT,
    @BaseUsers SMALLINT,
    @AdditionalUsers SMALLINT,
    @MaxUsers SMALLINT,
    @StripeCustomerId VARCHAR(50),
    @StripeSubscriptionId VARCHAR(50),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Organization]
    (
        [Id],
        [UserId],
        [Name],
        [BusinessName],
        [BillingEmail],
        [Plan],
        [PlanType],
        [PlanBasePrice],
        [PlanUserPrice],
        [PlanRenewalDate],
        [PlanTrial],
        [BaseUsers],
        [AdditionalUsers],
        [MaxUsers],
        [StripeCustomerId],
        [StripeSubscriptionId],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @BusinessName,
        @BillingEmail,
        @Plan,
        @PlanType,
        @PlanBasePrice,
        @PlanUserPrice,
        @PlanRenewalDate,
        @PlanTrial,
        @BaseUsers,
        @AdditionalUsers,
        @MaxUsers,
        @StripeCustomerId,
        @StripeSubscriptionId,
        @CreationDate,
        @RevisionDate
    )
END