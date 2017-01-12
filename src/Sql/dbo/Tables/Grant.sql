CREATE TABLE [dbo].[Grant] (
    [Key]            NVARCHAR (200) NOT NULL,
    [Type]           NVARCHAR (50)  NULL,
    [SubjectId]      NVARCHAR (50)  NULL,
    [ClientId]       NVARCHAR (50)  NOT NULL,
    [CreationDate]   DATETIME2 (7)  NOT NULL,
    [ExpirationDate] DATETIME2 (7)  NULL,
    [Data]           NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_Grant] PRIMARY KEY CLUSTERED ([Key] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Grant_SubjectId_ClientId_Type]
    ON [dbo].[Grant]([SubjectId] ASC, [ClientId] ASC, [Type] ASC);

