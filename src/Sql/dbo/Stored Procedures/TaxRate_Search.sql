CREATE PROCEDURE [dbo].[TaxRate_Search]
    @Skip INT = 0,
    @Count INT = 25
AS
BEGIN
    SET NOCOUNT ON 
    
    SELECT * FROM [dbo].[TaxRate]
    WHERE Active = 1
    ORDER BY Country, PostalCode DESC
    OFFSET @Skip ROWS
    FETCH NEXT @Count ROWS ONLY
END
GO
