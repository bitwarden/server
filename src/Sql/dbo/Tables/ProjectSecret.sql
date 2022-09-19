CREATE TABLE [ProjectSecret] (
    [Id] UNIQUEIDENTIFIER NOT NULL, 
    [ProjectId] UNIQUEIDENTIFIER NOT NULL,
    [SecretId] UNIQUEIDENTIFIER NOT NULL,
    CONSTRAINT [PK_ProjectSecret] PRIMARY KEY ([ProjectId], [SecretId]),
    CONSTRAINT [FK_ProjectSecret_Project_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Project] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProjectSecret_Secret_SecretId] FOREIGN KEY ([SecretId]) REFERENCES [Secret] ([Id]) ON DELETE CASCADE
);

GO