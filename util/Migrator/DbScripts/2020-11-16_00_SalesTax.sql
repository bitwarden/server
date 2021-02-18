IF OBJECT_ID('[dbo].[TaxRate]') IS NULL
BEGIN
    CREATE TABLE [dbo].[TaxRate] (
        [Id]                VARCHAR(40)         NOT NULL,
        [Country]           VARCHAR(50)         NOT NULL,
        [State]             VARCHAR(2)          NULL,
        [PostalCode]        VARCHAR(10)         NOT NULL,
        [Rate]              DECIMAL(5,2)        NOT NULL,
        [Active]            BIT                 NOT NULL,
        CONSTRAINT [PK_TaxRate] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE UNIQUE INDEX [IX_TaxRate_Country_PostalCode_Active_Uniqueness]
    ON [dbo].[TaxRate](Country, PostalCode)
    WHERE Active = 1;
END
GO

IF OBJECT_ID('[dbo].[TaxRate_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[TaxRate_ReadById]
END
GO

CREATE PROCEDURE [dbo].[TaxRate_ReadById]
    @Id VARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON 

    SELECT * FROM [dbo].[TaxRate]
    WHERE Id = @Id
END
GO

IF OBJECT_ID('[dbo].[TaxRate_Search]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[TaxRate_Search]
END
GO

CREATE PROCEDURE [dbo].[TaxRate_Search]
    @Skip INT = 0,
    @Count INT = 25
AS
BEGIN
    SET NOCOUNT ON 
    
    SELECT * FROM [dbo].[TaxRate]
    WHERE Active = 1
    ORDER BY Country, PostalCode DESC
    OFFSET @Skip ROWS
    FETCH NEXT @Count ROWS ONLY
END
GO

IF OBJECT_ID('[dbo].[TaxRate_ReadAllActive]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[TaxRate_ReadAllActive]
END
GO

CREATE PROCEDURE [dbo].[TaxRate_ReadAllActive]
AS
BEGIN
    SET NOCOUNT ON 
    
    SELECT * FROM [dbo].[TaxRate]
    WHERE Active = 1
END
GO

IF OBJECT_ID('[dbo].[TaxRate_ReadByLocation]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[TaxRate_ReadByLocation]
END
GO

CREATE PROCEDURE [dbo].[TaxRate_ReadByLocation]
    @Country VARCHAR(50),
    @PostalCode VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON 
    
    SELECT * FROM [dbo].[TaxRate]
    WHERE Active = 1
        AND [Country] = @Country
        AND [PostalCode] = @PostalCode
END
GO

IF OBJECT_ID('[dbo].[TaxRate_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[TaxRate_Create]
END
GO

CREATE PROCEDURE [dbo].[TaxRate_Create]
    @Id VARCHAR(40),
    @Country VARCHAR(50),
    @State VARCHAR(2),
    @PostalCode VARCHAR(10),
    @Rate DECIMAL(5,2),
    @Active BIT
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[TaxRate]
    (
        [Id],
        [Country],
        [State],
        [PostalCode],
        [Rate],
        [Active]
    )
    VALUES
    (
        @Id,
        @Country,
        @State,
        @PostalCode,
        @Rate,
        1
    )
END
GO

IF OBJECT_ID('[dbo].[TaxRate_Archive]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[TaxRate_Archive]
END
GO

CREATE PROCEDURE [dbo].[TaxRate_Archive]
    @Id VARCHAR(40)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[TaxRate]
    SET
        [Active] = 0
    WHERE
        [Id] = @Id
END
