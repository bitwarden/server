IF OBJECT_ID('dbo.akd_azks', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.akd_azks (
        akd_key SMALLINT NOT NULL CHECK (akd_key >= 0),
        epoch BIGINT NOT NULL CHECK (epoch >= 0),
        num_nodes BIGINT NOT NULL CHECK (num_nodes >= 0),
        PRIMARY KEY (akd_key)
    );
END
