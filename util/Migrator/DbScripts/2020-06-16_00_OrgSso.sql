IF OBJECT_ID('[dbo].[SsoConfig]') IS NULL
BEGIN
    CREATE TABLE [dbo].[SsoConfig] (
        [OrganizationId]     UNIQUEIDENTIFIER    NULL,
        [Identifier]         NVARCHAR (50)       NULL,
        [Data]               NVARCHAR (MAX)      NULL,
        [CreationDate]       DATETIME2 (7)       NOT NULL,
        [RevisionDate]       DATETIME2 (7)       NOT NULL,
        CONSTRAINT [FK_SsoConfig_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) 
    );
END
GO