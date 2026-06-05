-- Scope the provider client invoice report to the authorized provider (VULN-565 / PM-36574).
-- Adds a provider-scoped lookup so a provider can only read invoice items it owns.
CREATE OR ALTER PROCEDURE [dbo].[ProviderInvoiceItem_ReadByProviderIdAndInvoiceId]
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
GO
