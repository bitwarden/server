CREATE PROCEDURE [dbo].[Organization_Update]
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

    UPDATE
        [dbo].[Organization]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [BusinessName] = @BusinessName,
        [BillingEmail] = @BillingEmail,
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [PlanBasePrice] = @PlanBasePrice,
        [PlanUserPrice] = @PlanUserPrice,
        [PlanRenewalDate] = @PlanRenewalDate,
        [PlanTrial] = @PlanTrial,
        [BaseUsers] = @BaseUsers,
        [AdditionalUsers] = @AdditionalUsers,
        [MaxUsers] = @MaxUsers,
        [StripeCustomerId] = @StripeCustomerId,
        [StripeSubscriptionId] = @StripeSubscriptionId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END