CREATE PROCEDURE [dbo].[AutofillTriageReport_Update]
    @Id               UNIQUEIDENTIFIER,
    @PageUrl          NVARCHAR (1024),
    @TargetElementRef NVARCHAR (512),
    @UserMessage      NVARCHAR (200),
    @ReportData       NVARCHAR (MAX),
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
        [CreationDate]     = @CreationDate,
        [Archived]         = @Archived
    WHERE
        [Id] = @Id
END
