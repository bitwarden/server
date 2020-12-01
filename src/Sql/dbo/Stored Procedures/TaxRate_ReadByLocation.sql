CREATE PROCEDURE [dbo].[TaxRate_ReadByLocation]
    @Country VARCHAR(50),
    @PostalCode VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON 
    
    SELECT * FROM [dbo].[TaxRate]
    WHERE Active = 1
        AND [Country] = @Country
        AND [PostalCode] = @PostalCode
END
