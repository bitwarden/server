CREATE PROCEDURE [dbo].[ProviderInvoiceItem_ReadByInvoiceId]
    @InvoiceId VARCHAR (50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderInvoiceItemView]
    WHERE
        [InvoiceId] = @InvoiceId
END
