IF OBJECT_ID('[dbo].[TaxRate]') IS NOT NULL
BEGIN
    ALTER TABLE 
        [dbo].[TaxRate]
    ALTER COLUMN 
        [Rate] DECIMAL(6,3) NOT NULL;
END
