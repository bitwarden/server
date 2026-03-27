CREATE PROCEDURE [dbo].[AutofillTriageReport_Create]
    @Id               UNIQUEIDENTIFIER OUTPUT,
    @PageUrl          NVARCHAR (1024),
    @TargetElementRef NVARCHAR (512),
    @UserMessage      NVARCHAR (200),
    @ReportData       NVARCHAR (MAX),
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
        @CreationDate,
        @Archived
    )
END
