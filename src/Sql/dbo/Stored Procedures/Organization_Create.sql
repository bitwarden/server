CREATE PROCEDURE [dbo].[Organization_Create]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BillingEmail NVARCHAR(50),
    @Plan NVARCHAR(50),
    @PlanType TINYINT,
    @Seats SMALLINT,
    @MaxCollections SMALLINT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @Storage BIGINT,
    @MaxStorageGb SMALLINT,
    @StripeCustomerId VARCHAR(50),
    @StripeSubscriptionId VARCHAR(50),
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Organization]
    (
        [Id],
        [Name],
        [BusinessName],
        [BillingEmail],
        [Plan],
        [PlanType],
        [Seats],
        [MaxCollections],
        [UseGroups],
        [UseDirectory],
        [Storage],
        [MaxStorageGb],
        [StripeCustomerId],
        [StripeSubscriptionId],
        [Enabled],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Name,
        @BusinessName,
        @BillingEmail,
        @Plan,
        @PlanType,
        @Seats,
        @MaxCollections,
        @UseGroups,
        @UseDirectory,
        @Storage,
        @MaxStorageGb,
        @StripeCustomerId,
        @StripeSubscriptionId,
        @Enabled,
        @CreationDate,
        @RevisionDate
    )
END