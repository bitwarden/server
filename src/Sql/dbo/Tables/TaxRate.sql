CREATE TABLE [dbo].[TaxRate] (
    [Id]                VARCHAR(40)         NOT NULL,
    [Country]           VARCHAR(50)         NOT NULL,
    [State]             VARCHAR(2)          NULL,
    [PostalCode]        VARCHAR(10)         NOT NULL,
    [Rate]              DECIMAL(6,3)        NOT NULL,
    [Active]            BIT                 NOT NULL,
    CONSTRAINT [PK_TaxRate] PRIMARY KEY CLUSTERED ([Id] ASC)
);
GO

CREATE UNIQUE INDEX [IX_TaxRate_Country_PostalCode_Active_Uniqueness]
ON [dbo].[TaxRate](Country, PostalCode)
WHERE Active = 1;
