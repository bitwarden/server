ALTER TABLE
  [dbo].[AuthRequest]
ADD
  [Region] NVARCHAR(50) NULL,
  [CountryName] NVARCHAR(256) NULL,
  [CityName] NVARCHAR(256) NULL;