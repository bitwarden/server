IF OBJECT_ID('[dbo].[Secret]') IS NULL
BEGIN
    CREATE TABLE [dbo].[Secret] (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Key] NVARCHAR(MAX) NULL,
        [Value] NVARCHAR(MAX) NULL,
        [Note] NVARCHAR(MAX) NULL,
        [CreationDate] DATETIME2(7) NOT NULL,
        [RevisionDate] DATETIME2(7) NOT NULL,
        [DeletedDate] DATETIME2(7) NULL,
        CONSTRAINT [PK_Secret] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Secret_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]) ON DELETE CASCADE
        );

    CREATE NONCLUSTERED INDEX [IX_Secret_OrganizationId] ON [dbo].[Secret] ([OrganizationId] ASC);

    CREATE NONCLUSTERED INDEX [IX_Secret_DeletedDate] ON [dbo].[Secret] ([DeletedDate] ASC);
END
GO
