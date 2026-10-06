IF OBJECT_ID('[dbo].[DataMigrationState]') IS NULL
BEGIN
    CREATE TABLE [dbo].[DataMigrationState] (
        [Id]                UNIQUEIDENTIFIER NOT NULL,
        [Name]              VARCHAR(100)     NOT NULL,
        [Partition]         INT              NOT NULL,
        [RangeStart]        NVARCHAR(300)    NOT NULL,
        [RangeEnd]          NVARCHAR(300)    NOT NULL,
        [TotalRows]         BIGINT           NOT NULL,
        [Cursor]            NVARCHAR(300)    NOT NULL,
        [RowsScanned]       BIGINT           NOT NULL,
        [RowsConverted]     BIGINT           NOT NULL,
        [RowsSkippedByRace] BIGINT           NOT NULL,
        [RowsFailed]        BIGINT           NOT NULL,
        [LeaseOwner]        NVARCHAR(100)    NULL,
        [LeaseExpiresDate]  DATETIME2(7)     NULL,
        [PausedDate]        DATETIME2(7)     NULL,
        [StartedDate]       DATETIME2(7)     NULL,
        [CompletedDate]     DATETIME2(7)     NULL,
        [CreationDate]      DATETIME2(7)     NOT NULL,
        [RevisionDate]      DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_DataMigrationState] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE [name] = 'IX_DataMigrationState_Name_Partition')
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_DataMigrationState_Name_Partition]
        ON [dbo].[DataMigrationState]([Name] ASC, [Partition] ASC);
END
GO
