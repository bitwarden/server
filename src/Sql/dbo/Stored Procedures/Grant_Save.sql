CREATE PROCEDURE [dbo].[Grant_Save]
    @Key  NVARCHAR(200),
    @Type  NVARCHAR(50),
    @SubjectId NVARCHAR(50),
    @ClientId NVARCHAR(50),
    @CreationDate DATETIME2,
    @ExpirationDate DATETIME2,
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
            @ClientId,
            @CreationDate,
            @ExpirationDate,
            @Data
        )
    ) AS [Source]
    (
        [Key],
        [Type],
        [SubjectId],
        [ClientId],
        [CreationDate],
        [ExpirationDate],
        [Data]
    ) 
    ON
        [Target].[Key] = [Source].[Key]
    WHEN MATCHED THEN
        UPDATE
        SET
            [Type] = [Source].[Type],
            [SubjectId] = [Source].[SubjectId],
            [ClientId] = [Source].[ClientId],
            [CreationDate] = [Source].[CreationDate],
            [ExpirationDate] = [Source].[ExpirationDate],
            [Data] = [Source].[Data]
    WHEN NOT MATCHED THEN
        INSERT
        (
            [Key],
            [Type],
            [SubjectId],
            [ClientId],
            [CreationDate],
            [ExpirationDate],
            [Data]
        )
        VALUES
        (
            [Source].[Key],
            [Source].[Type],
            [Source].[SubjectId],
            [Source].[ClientId],
            [Source].[CreationDate],
            [Source].[ExpirationDate],
            [Source].[Data]
        )
    ;
END
