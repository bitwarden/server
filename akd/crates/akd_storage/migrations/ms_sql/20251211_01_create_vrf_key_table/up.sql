IF OBJECT_ID('dbo.vrf_key', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.vrf_key (
        root_key_hash VARBINARY(32) NOT NULL,
        root_key_type INT NOT NULL,
        enc_sym_key VARBINARY(32) NULL,
        enc_sym_key_nonce VARBINARY(24) NULL,
        sym_enc_vrf_key VARBINARY(32) NOT NULL,
        PRIMARY KEY (root_key_hash, root_key_type)
    );
END
