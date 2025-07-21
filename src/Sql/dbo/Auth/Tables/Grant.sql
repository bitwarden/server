CREATE TABLE [dbo].[Grant]
(
    [Id] INT NOT NULL IDENTITY(1,1),
    [Key] NVARCHAR (200) NOT NULL,
    [Type] NVARCHAR (50) NOT NULL,
    [SubjectId] NVARCHAR (200) NULL,
    [SessionId] NVARCHAR (100) NULL,
    [ClientId] NVARCHAR (200) NOT NULL,
    [Description] NVARCHAR (200) NULL,
    [CreationDate] DATETIME2 (7) NOT NULL,
    [ExpirationDate] DATETIME2 (7) NULL,
    [ConsumedDate] DATETIME2 (7) NULL,
    [Data] NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_Grant] PRIMARY KEY CLUSTERED ([Id] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Grant_ExpirationDate]
    ON [dbo].[Grant]([ExpirationDate] ASC);

GO

CREATE UNIQUE INDEX [IX_Grant_Key]
    ON [dbo].[Grant]([Key]);
