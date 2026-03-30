IF OBJECT_ID('[dbo].[AutofillTriageReport]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AutofillTriageReport] (
        [Id]               UNIQUEIDENTIFIER NOT NULL,
        [PageUrl]          NVARCHAR (1024)  NOT NULL,
        [TargetElementRef] NVARCHAR (512)   NULL,
        [UserMessage]      NVARCHAR (200)   NULL,
        [ReportData]       NVARCHAR (MAX)   NOT NULL,
        [ExtensionVersion] NVARCHAR (50)    NOT NULL,
        [CreationDate]     DATETIME2 (7)    NOT NULL,
        [Archived]         BIT              NOT NULL CONSTRAINT [DF_AutofillTriageReport_Archived] DEFAULT (0),
        CONSTRAINT [PK_AutofillTriageReport] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_AutofillTriageReport_CreationDate]
        ON [dbo].[AutofillTriageReport] ([Archived] ASC, [CreationDate] DESC);
END
GO

CREATE OR ALTER VIEW [dbo].[AutofillTriageReportView]
AS
SELECT
    *
FROM
    [dbo].[AutofillTriageReport]
GO

CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_Create]
    @Id               UNIQUEIDENTIFIER OUTPUT,
    @PageUrl          NVARCHAR (1024),
    @TargetElementRef NVARCHAR (512),
    @UserMessage      NVARCHAR (200),
    @ReportData       NVARCHAR (MAX),
    @ExtensionVersion NVARCHAR (50),
    @CreationDate     DATETIME2 (7),
    @Archived         BIT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AutofillTriageReport]
    (
        [Id],
        [PageUrl],
        [TargetElementRef],
        [UserMessage],
        [ReportData],
        [ExtensionVersion],
        [CreationDate],
        [Archived]
    )
    VALUES
    (
        @Id,
        @PageUrl,
        @TargetElementRef,
        @UserMessage,
        @ReportData,
        @ExtensionVersion,
        @CreationDate,
        @Archived
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_Update]
    @Id               UNIQUEIDENTIFIER,
    @PageUrl          NVARCHAR (1024),
    @TargetElementRef NVARCHAR (512),
    @UserMessage      NVARCHAR (200),
    @ReportData       NVARCHAR (MAX),
    @ExtensionVersion NVARCHAR (50),
    @CreationDate     DATETIME2 (7),
    @Archived         BIT
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AutofillTriageReport]
    SET
        [PageUrl]          = @PageUrl,
        [TargetElementRef] = @TargetElementRef,
        [UserMessage]      = @UserMessage,
        [ReportData]       = @ReportData,
        [ExtensionVersion] = @ExtensionVersion,
        [CreationDate]     = @CreationDate,
        [Archived]         = @Archived
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[AutofillTriageReport]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AutofillTriageReportView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_ReadActiveWithPagination]
    @Skip INT = 0,
    @Take INT = 25
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AutofillTriageReportView]
    WHERE
        [Archived] = 0
    ORDER BY
        [CreationDate] DESC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_Archive]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AutofillTriageReport]
    SET
        [Archived] = 1
    WHERE
        [Id] = @Id
END
GO
