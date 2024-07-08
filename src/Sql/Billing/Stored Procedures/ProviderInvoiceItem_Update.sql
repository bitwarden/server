CREATE PROCEDURE [dbo].[ProviderInvoiceItem_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @InvoiceId VARCHAR (50),
    @InvoiceNumber VARCHAR (50),
    @ClientName NVARCHAR (50),
    @PlanName NVARCHAR (50),
    @AssignedSeats INT,
    @UsedSeats INT,
    @Total MONEY,
    @Created DATETIME2 (7) = NULL,
    @ClientId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    SET @Created = COALESCE(@Created, GETUTCDATE())

    UPDATE
        [dbo].[ProviderInvoiceItem]
    SET
        [ProviderId] = @ProviderId,
        [InvoiceId] = @InvoiceId,
        [InvoiceNumber] = @InvoiceNumber,
        [ClientName] = @ClientName,
        [PlanName] = @PlanName,
        [AssignedSeats] = @AssignedSeats,
        [UsedSeats] = @UsedSeats,
        [Total] = @Total,
        [Created] = @Created,
        [ClientId] = @ClientId
    WHERE
        [Id] = @Id
END
