IF OBJECT_ID('[dbo].[ProjectSecret]') IS NULL
BEGIN
CREATE TABLE [ProjectSecret] (
    [ProjectsId] UNIQUEIDENTIFIER NOT NULL,
    [SecretsId]  UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_ProjectSecret] PRIMARY KEY ([ProjectsId], [SecretsId]),
    CONSTRAINT [FK_ProjectSecret_Project_ProjectsId] FOREIGN KEY ([ProjectsId]) REFERENCES [Project] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectSecret_Secret_SecretsId] FOREIGN KEY ([SecretsId]) REFERENCES [Secret] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_ProjectSecret_SecretsId] ON [ProjectSecret] ([SecretsId]);

END

GO
