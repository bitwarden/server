CREATE TABLE [dbo].[AutofillTriageReport] (
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [PageUrl]          NVARCHAR (1024)  NOT NULL,
    [TargetElementRef] NVARCHAR (512)   NULL,
    [UserMessage]      NVARCHAR (200)   NULL,
    [ReportData]       NVARCHAR (MAX)   NOT NULL,
    [CreationDate]     DATETIME2 (7)    NOT NULL,
    [Archived]         BIT              NOT NULL CONSTRAINT [DF_AutofillTriageReport_Archived] DEFAULT (0),
    CONSTRAINT [PK_AutofillTriageReport] PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE NONCLUSTERED INDEX [IX_AutofillTriageReport_CreationDate]
    ON [dbo].[AutofillTriageReport] ([Archived] ASC, [CreationDate] DESC);
