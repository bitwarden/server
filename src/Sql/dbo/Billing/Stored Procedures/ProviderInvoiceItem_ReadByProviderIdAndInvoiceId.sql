CREATE PROCEDURE [dbo].[ProviderInvoiceItem_ReadByProviderIdAndInvoiceId]
    @ProviderId UNIQUEIDENTIFIER,
    @InvoiceId VARCHAR (50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderInvoiceItemView]
    WHERE
        [ProviderId] = @ProviderId
        AND [InvoiceId] = @InvoiceId
END
