use akd::errors::StorageError;
use tracing::debug;

use crate::{
    ms_sql::{
        migrations::TABLE_VRF_KEYS,
        sql_params::SqlParams,
        tables::akd_storable_for_ms_sql::{QueryStatement, Statement},
    },
    vrf_key_config::{VrfKeyConfig, VrfRootKeyError},
    vrf_key_database::VrfKeyTableData,
};

pub fn get_statement(
    config: &VrfKeyConfig,
) -> Result<QueryStatement<VrfKeyTableData>, VrfRootKeyError> {
    debug!("Building get_statement for vrf key");
    let mut params = SqlParams::new();
    params.add("root_key_type", Box::new(config.root_key_type() as i16));
    params.add(
        "root_key_hash",
        Box::new(config.root_key_hash().expect("valid root key hash")),
    );

    let sql = format!(
        r#"
        SELECT root_key_hash, root_key_type, enc_sym_key, sym_enc_vrf_key, sym_enc_vrf_key_nonce
        FROM {}
        WHERE root_key_type = {} AND root_key_hash = {}"#,
        TABLE_VRF_KEYS,
        params
            .key_for("root_key_type")
            .expect("root_key_type was added to the params list"),
        params
            .key_for("root_key_hash")
            .expect("root_key_hash was added to the params list"),
    );
    Ok(QueryStatement::new(sql, params, from_row))
}

pub fn from_row(row: &ms_database::Row) -> Result<VrfKeyTableData, StorageError> {
    let root_key_hash: &[u8] = row
        .get("root_key_hash")
        .ok_or_else(|| StorageError::Other("Missing root_key_hash column".to_string()))?;
    let root_key_type: i16 = row
        .get("root_key_type")
        .ok_or_else(|| StorageError::Other("root_key_type is NULL or missing".to_string()))?;
    let enc_sym_key: Option<&[u8]> = row.get("enc_sym_key");
    let sym_enc_vrf_key: &[u8] = row
        .get("sym_enc_vrf_key")
        .ok_or_else(|| StorageError::Other("sym_enc_vrf_key is NULL or missing".to_string()))?;
    let sym_enc_vrf_key_nonce: &[u8] = row.get("sym_enc_vrf_key_nonce").ok_or_else(|| {
        StorageError::Other("sym_enc_vrf_key_nonce is NULL or missing".to_string())
    })?;

    Ok(VrfKeyTableData {
        root_key_hash: root_key_hash.to_vec(),
        root_key_type: root_key_type.into(),
        enc_sym_key: enc_sym_key.map(|k| k.to_vec()),
        sym_enc_vrf_key: sym_enc_vrf_key.to_vec(),
        sym_enc_vrf_key_nonce: sym_enc_vrf_key_nonce.to_vec(),
    })
}

pub fn store_statement(table_data: &VrfKeyTableData) -> Statement {
    debug!("Building store_statement for vrf key");
    let mut params = SqlParams::new();
    params.add("root_key_hash", Box::new(table_data.root_key_hash.clone()));
    params.add(
        "root_key_type",
        Box::new(Into::<i16>::into(table_data.root_key_type)),
    );
    params.add("enc_sym_key", Box::new(table_data.enc_sym_key.clone()));
    params.add(
        "sym_enc_vrf_key",
        Box::new(table_data.sym_enc_vrf_key.clone()),
    );
    params.add(
        "sym_enc_vrf_key_nonce",
        Box::new(table_data.sym_enc_vrf_key_nonce.clone()),
    );
    let sql = format!(
        r#"
        MERGE INTO dbo.{TABLE_VRF_KEYS} AS t
        USING (SELECT {}) AS source
        ON t.root_key_hash = source.root_key_hash AND t.root_key_type = source.root_key_type
        WHEN MATCHED THEN
            UPDATE SET {}
        WHEN NOT MATCHED THEN
            INSERT ({})
            VALUES ({});"#,
        params.keys_as_columns().join(", "),
        params
            .set_columns_equal_except("t.", "source.", vec!["root_key_hash", "root_key_type"])
            .join(", "),
        params.columns().join(", "),
        params.keys().join(", "),
    );

    Statement::new(sql, params)
}
