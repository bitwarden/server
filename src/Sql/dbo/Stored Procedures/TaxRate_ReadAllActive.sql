CREATE PROCEDURE [dbo].[TaxRate_ReadAllActive]
AS
BEGIN
    SET NOCOUNT ON 
    
    SELECT * FROM [dbo].[TaxRate]
    WHERE Active = 1
END
GO
