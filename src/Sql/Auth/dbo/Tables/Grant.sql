CREATE TABLE [dbo].[Grant] (
    [Key]            NVARCHAR (200) NOT NULL,
    [Type]           NVARCHAR (50)  NOT NULL,
    [SubjectId]      NVARCHAR (200) NULL,
    [SessionId]      NVARCHAR (100) NULL,
    [ClientId]       NVARCHAR (200) NOT NULL,
    [Description]    NVARCHAR (200) NULL,
    [CreationDate]   DATETIME2 (7)  NOT NULL,
    [ExpirationDate] DATETIME2 (7)  NULL,
    [ConsumedDate]   DATETIME2 (7)  NULL,
    [Data]           NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_Grant] PRIMARY KEY CLUSTERED ([Key] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_Grant_SubjectId_ClientId_Type]
    ON [dbo].[Grant]([SubjectId] ASC, [ClientId] ASC, [Type] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_Grant_SubjectId_SessionId_Type]
    ON [dbo].[Grant]([SubjectId] ASC, [SessionId] ASC, [Type] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_Grant_ExpirationDate]
    ON [dbo].[Grant]([ExpirationDate] ASC);

