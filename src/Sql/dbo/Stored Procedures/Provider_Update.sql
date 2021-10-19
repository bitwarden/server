CREATE PROCEDURE [dbo].[Provider_Update]
    @Id UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @BusinessName NVARCHAR(50),
    @BusinessAddress1 NVARCHAR(50),
    @BusinessAddress2 NVARCHAR(50),
    @BusinessAddress3 NVARCHAR(50),
    @BusinessCountry VARCHAR(2),
    @BusinessTaxNumber NVARCHAR(30),
    @BillingEmail NVARCHAR(256),
    @Status TINYINT,
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
        [Status] = @Status,
        [UseEvents] = @UseEvents,
        [Enabled] = @Enabled,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
