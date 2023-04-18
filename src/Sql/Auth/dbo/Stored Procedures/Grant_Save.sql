CREATE PROCEDURE [dbo].[Grant_Save]
    @Key  NVARCHAR(200),
    @Type  NVARCHAR(50),
    @SubjectId NVARCHAR(200),
    @SessionId NVARCHAR(100),
    @ClientId NVARCHAR(200),
    @Description NVARCHAR(200),
    @CreationDate DATETIME2,
    @ExpirationDate DATETIME2,
    @ConsumedDate DATETIME2,
    @Data NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON

    MERGE
        [dbo].[Grant] AS [Target]
    USING
    ( 
        VALUES
        (
            @Key,
            @Type,
            @SubjectId,
            @SessionId,
            @ClientId,
            @Description,
            @CreationDate,
            @ExpirationDate,
            @ConsumedDate,
            @Data
        )
    ) AS [Source]
    (
        [Key],
        [Type],
        [SubjectId],
        [SessionId],
        [ClientId],
        [Description],
        [CreationDate],
        [ExpirationDate],
        [ConsumedDate],
        [Data]
    ) 
    ON
        [Target].[Key] = [Source].[Key]
    WHEN MATCHED THEN
        UPDATE
        SET
            [Type] = [Source].[Type],
            [SubjectId] = [Source].[SubjectId],
            [SessionId] = [Source].[SessionId],
            [ClientId] = [Source].[ClientId],
            [Description] = [Source].[Description],
            [CreationDate] = [Source].[CreationDate],
            [ExpirationDate] = [Source].[ExpirationDate],
            [ConsumedDate] = [Source].[ConsumedDate],
            [Data] = [Source].[Data]
    WHEN NOT MATCHED THEN
        INSERT
        (
            [Key],
            [Type],
            [SubjectId],
            [SessionId],
            [ClientId],
            [Description],
            [CreationDate],
            [ExpirationDate],
            [ConsumedDate],
            [Data]
        )
        VALUES
        (
            [Source].[Key],
            [Source].[Type],
            [Source].[SubjectId],
            [Source].[SessionId],
            [Source].[ClientId],
            [Source].[Description],
            [Source].[CreationDate],
            [Source].[ExpirationDate],
            [Source].[ConsumedDate],
            [Source].[Data]
        )
    ;
END
