CREATE TABLE [dbo].[ProjectSecret] (
    [ProjectsId] uniqueidentifier NOT NULL,
    [SecretsId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ProjectSecret] PRIMARY KEY ([ProjectsId], [SecretsId]),
    CONSTRAINT [FK_ProjectSecret_Project_ProjectsId] FOREIGN KEY ([ProjectsId]) REFERENCES [Project] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectSecret_Secret_SecretsId] FOREIGN KEY ([SecretsId]) REFERENCES [Secret] ([Id]) ON DELETE CASCADE
);

GO
CREATE INDEX [IX_ProjectSecret_SecretsId] ON [ProjectSecret] ([SecretsId]);
