CREATE OR ALTER PROCEDURE [dbo].[Grant_Save]
    @Key NVARCHAR(200),
    @Type NVARCHAR(50),
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

    -- First, try to update the existing row
    UPDATE [dbo].[Grant]
    SET
        [Type] = @Type,
        [SubjectId] = @SubjectId,
        [SessionId] = @SessionId,
        [ClientId] = @ClientId,
        [Description] = @Description,
        [CreationDate] = @CreationDate,
        [ExpirationDate] = @ExpirationDate,
        [ConsumedDate] = @ConsumedDate,
        [Data] = @Data
    WHERE
        [Key] = @Key

    -- If no row was updated, insert a new one
    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[Grant]
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
    END
END
