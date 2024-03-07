CREATE PROCEDURE [dbo].[Provider_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @BillingPhone NVARCHAR(50) = NULL,
    @Status TINYINT,
    @Type TINYINT = 0,
    @UseEvents BIT,
    @Enabled BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @Gateway TINYINT = 0,
    @GatewayCustomerId VARCHAR(50) = NULL,
    @GatewaySubscriptionId VARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Provider]
    (
        [Id],
        [Name],
        [BusinessName],
        [BusinessAddress1],
        [BusinessAddress2],
        [BusinessAddress3],
        [BusinessCountry],
        [BusinessTaxNumber],
        [BillingEmail],
        [BillingPhone],
        [Status],
        [Type],
        [UseEvents],
        [Enabled],
        [CreationDate],
        [RevisionDate],
        [Gateway],
        [GatewayCustomerId],
        [GatewaySubscriptionId]
    )
    VALUES
    (
        @Id,
        @Name,
        @BusinessName,
        @BusinessAddress1,
        @BusinessAddress2,
        @BusinessAddress3,
        @BusinessCountry,
        @BusinessTaxNumber,
        @BillingEmail,
        @BillingPhone,
        @Status,
        @Type,
        @UseEvents,
        @Enabled,
        @CreationDate,
        @RevisionDate,
        @Gateway,
        @GatewayCustomerId,
        @GatewaySubscriptionId
    )
END
