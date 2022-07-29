CREATE PROCEDURE [dbo].[Organization_Update]
    @Id UNIQUEIDENTIFIER,
    @Identifier NVARCHAR(50),
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @Plan NVARCHAR(50) = NULL,
    @PlanType TINYINT = NULL,
    @Seats INT = 0,
    @MaxCollections SMALLINT,
    @UsePolicies BIT,
    @UseSso BIT,
    @UseGroups BIT,
    @UseDirectory BIT,
    @UseEvents BIT,
    @UseTotp BIT = 0,
    @Use2fa BIT,
    @UseApi BIT,
    @UseResetPassword BIT,
    @SelfHost BIT,
    @UsersGetPremium BIT = 0,
    @Storage BIGINT = 0,
    @MaxStorageGb SMALLINT = 0,
    @Gateway TINYINT,
    @GatewayCustomerId VARCHAR(50),
    @GatewaySubscriptionId VARCHAR(50),
    @ReferenceData VARCHAR(MAX),
    @Enabled BIT,
    @LicenseKey VARCHAR(100),
    @PublicKey VARCHAR(MAX),
    @PrivateKey VARCHAR(MAX),
    @TwoFactorProviders NVARCHAR(MAX),
    @ExpirationDate DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @OwnersNotifiedOfAutoscaling DATETIME2(7),
    @MaxAutoscaleSeats INT = 0,
    @UseKeyConnector BIT = 0,
    @UseScim BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Organization]
    SET
        [Identifier] = @Identifier,
        [Name] = @Name,
        [BusinessName] = @BusinessName,
        [BusinessAddress1] = @BusinessAddress1,
        [BusinessAddress2] = @BusinessAddress2,
        [BusinessAddress3] = @BusinessAddress3,
        [BusinessCountry] = @BusinessCountry,
        [BusinessTaxNumber] = @BusinessTaxNumber,
        [BillingEmail] = @BillingEmail,
        [Plan] = @Plan,
        [PlanType] = @PlanType,
        [Seats] = @Seats,
        [MaxCollections] = @MaxCollections,
        [UsePolicies] = @UsePolicies,
        [UseSso] = @UseSso,
        [UseGroups] = @UseGroups,
        [UseDirectory] = @UseDirectory,
        [UseEvents] = @UseEvents,
        [UseTotp] = @UseTotp,
        [Use2fa] = @Use2fa,
        [UseApi] = @UseApi,
        [UseResetPassword] = @UseResetPassword,
        [SelfHost] = @SelfHost,
        [UsersGetPremium] = @UsersGetPremium,
        [Storage] = @Storage,
        [MaxStorageGb] = @MaxStorageGb,
        [Gateway] = @Gateway,
        [GatewayCustomerId] = @GatewayCustomerId,
        [GatewaySubscriptionId] = @GatewaySubscriptionId,
        [ReferenceData] = @ReferenceData,
        [Enabled] = @Enabled,
        [LicenseKey] = @LicenseKey,
        [PublicKey] = @PublicKey,
        [PrivateKey] = @PrivateKey,
        [TwoFactorProviders] = @TwoFactorProviders,
        [ExpirationDate] = @ExpirationDate,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [OwnersNotifiedOfAutoscaling] = @OwnersNotifiedOfAutoscaling,
        [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
        [UseKeyConnector] = @UseKeyConnector,
        [UseScim] = @UseScim
    WHERE
        [Id] = @Id

    IF @Plan is not null
    BEGIN
        IF EXISTS(SELECT * FROM OrganizationPasswordManager O WHERE O.OrganizationId = @Id) 
            BEGIN
                UPDATE
                    [dbo].[OrganizationPasswordManager]
                SET
                    [Plan] = @Plan,
                    [PlanType] = @PlanType,
                    [Seats] = @Seats,
                    [MaxCollections] = @MaxCollections,
                    [UseTotp] = @UseTotp,
                    [UsersGetPremium] = @UsersGetPremium,
                    [Storage] = @Storage,
                    [MaxStorageGb] = @MaxStorageGb,
                    [MaxAutoscaleSeats] = @MaxAutoscaleSeats,
                    [RevisionDate] = GETUTCDATE()
                WHERE
                    [OrganizationId] = @Id
            END
        ELSE 
            BEGIN 
                INSERT INTO [dbo].[OrganizationPasswordManager]
                    (
                        [OrganizationId],
                        [Plan],
                        [PlanType],
                        [Seats],
                        [MaxCollections],
                        [UseTotp],
                        [UsersGetPremium],
                        [Storage],
                        [MaxStorageGb],
                        [MaxAutoscaleSeats],
                        [RevisionDate]
                    )
                    VALUES
                    (
                        @Id,
                        @Plan,
                        @PlanType,
                        @Seats,
                        @MaxCollections,
                        @UseTotp,
                        @UsersGetPremium,
                        @Storage,
                        @MaxStorageGb,
                        @MaxAutoscaleSeats,
                        GETUTCDATE()
                    )
            END 
    END
    
END
