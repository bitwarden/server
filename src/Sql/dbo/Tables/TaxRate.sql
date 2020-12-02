CREATE TABLE [dbo].[TaxRate] (
    [Id]                VARCHAR(40)         NOT NULL        PRIMARY KEY,
    [Country]           VARCHAR(50)         NOT NULL,
    [State]             VARCHAR(2)          NULL,
    [PostalCode]        VARCHAR(10)         NOT NULL,
    [Rate]              DECIMAL(5,2)        NOT NULL,
    [Active]        BIT                 NOT NULL
);
GO

ALTER TABLE [dbo].[TaxRate]
ADD CONSTRAINT Unique_Country_PostalCode
UNIQUE NONCLUSTERED (Country,PostalCode) 
