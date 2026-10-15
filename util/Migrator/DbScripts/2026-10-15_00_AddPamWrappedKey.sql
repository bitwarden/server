-- Adds a nullable WrappedKey column to PamTargetSystem and PamRotationConfig.
-- Existing rows keep NULL and must be re-saved. The procedures that read and write it come from
-- 2026-10-15_01_ReshapeUatPamRotation.sql and 2026-10-15_02_RestoreUatPamWrappedKey.sql.

IF COL_LENGTH('[dbo].[PamTargetSystem]', 'WrappedKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[PamTargetSystem] ADD [WrappedKey] NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('[dbo].[PamRotationConfig]', 'WrappedKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[PamRotationConfig] ADD [WrappedKey] NVARCHAR(MAX) NULL;
END
GO
