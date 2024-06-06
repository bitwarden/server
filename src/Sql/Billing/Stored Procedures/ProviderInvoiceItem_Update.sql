CREATE PROCEDURE [dbo].[ProviderInvoiceItem_Update]
    @Id UNIQUEIDENTIFIER,
    @ProviderId UNIQUEIDENTIFIER,
    @InvoiceId VARCHAR (50),
    @InvoiceNumber VARCHAR (50),
    @ClientName NVARCHAR (50),
    @PlanName NVARCHAR (50),
    @AssignedSeats INT,
    @UsedSeats INT,
    @Total MONEY
AS
BEGIN
    SET NOCOUNT ON

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
        [Total] = @Total
    WHERE
        [Id] = @Id
END
