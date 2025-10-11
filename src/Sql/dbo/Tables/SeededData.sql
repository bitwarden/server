CREATE TABLE [dbo].[SeededData] (
    [Id]           UNIQUEIDENTIFIER NOT NULL,
    [RecipeName]   NVARCHAR (MAX)   NOT NULL,
    [Data]         NVARCHAR (MAX)   NULL,
    [CreationDate] DATETIME2 (7)    NOT NULL,
);
