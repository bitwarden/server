CREATE TABLE [dbo].[Installation] (
    [Id]               UNIQUEIDENTIFIER  NOT NULL,
    [Email]            NVARCHAR (256)    NOT NULL,
    [Key]              VARCHAR (150)     NOT NULL,
    [Enabled]          BIT               NOT NULL,
    [CreationDate]     DATETIME2 (7)     NOT NULL,
    [LastActivityDate] DATETIME2 (7)     NULL,
    CONSTRAINT [PK_Installation] PRIMARY KEY CLUSTERED ([Id] ASC)
);

