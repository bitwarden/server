UPDATE PROCEDURE [dbo].[Provider_Update]
    @Id UNIQUEIDENTIFIER,
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
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[Provider]
SET
    [Name] = @Name,
    [BusinessName] = @BusinessName,
    [BusinessAddress1] = @BusinessAddress1,
    [BusinessAddress2] = @BusinessAddress2,
    [BusinessAddress3] = @BusinessAddress3,
    [BusinessCountry] = @BusinessCountry,
    [BusinessTaxNumber] = @BusinessTaxNumber,
    [BillingEmail] = @BillingEmail,
    [BillingPhone] = @BillingPhone,
    [Status] = @Status,
    [Type] = @Type,
    [UseEvents] = @UseEvents,
    [Enabled] = @Enabled,
    [CreationDate] = @CreationDate,
    [RevisionDate] = @RevisionDate
WHERE
    [Id] = @Id

    IF @Type = 1 -- Reseller Provider: Update all assigned organization's BillingEmail
    BEGIN
        UPDATE O
        SET O.[BillingEmail] = @BillingEmail
        FROM [dbo].[Organization] AS O
        INNER JOIN [dbo].[ProviderOrganization] AS PO
            ON PO.[OrganizationId] = O.[Id]
        WHERE PO.[ProviderId] = @Id
    END
END
