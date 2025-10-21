use akd::{
    errors::StorageError,
    storage::types::{DbRecord, ValueState, ValueStateRetrievalFlag},
    AkdLabel, AkdValue,
};
use ms_database::Row;

use crate::{migrations::TABLE_VALUES, ms_sql_storable::{QueryStatement, Statement}, sql_params::SqlParams};

pub fn get_all(raw_label: &AkdLabel) -> QueryStatement<ValueState> {
    let mut params = SqlParams::new();
    // the raw vector is the key for value storage
    params.add("raw_label", Box::new(raw_label.0.clone()));

    let sql = format!(
        r#"
        SELECT raw_label, epoch, version, node_label_val, node_label_len, data
        FROM {}
        WHERE raw_label = {}
        "#,
        TABLE_VALUES, params
            .key_for("raw_label")
            .expect("raw_label was added to the params list")
    );
    QueryStatement::new(sql, params, from_row)
}

pub fn get_by_flag(raw_label: &AkdLabel, flag: ValueStateRetrievalFlag) -> QueryStatement<ValueState> {
    let mut params = SqlParams::new();
    params.add("raw_label", Box::new(raw_label.0.clone()));

    match flag {
        ValueStateRetrievalFlag::SpecificEpoch(epoch)
        | ValueStateRetrievalFlag::LeqEpoch(epoch) => params.add("epoch", Box::new(epoch as i64)),
        ValueStateRetrievalFlag::SpecificVersion(version) => {
            params.add("version", Box::new(version as i64))
        }
        _ => {}
    }

    let mut sql = format!(
        r#"
        SELECT TOP(1) raw_label, epoch, version, node_label_val, node_label_len, data
        FROM {}
        WHERE raw_label = {}
        "#,
        TABLE_VALUES,
        params
            .key_for("raw_label")
            .expect("raw_label was added to the params list")
    );

    match flag {
        ValueStateRetrievalFlag::SpecificEpoch(_) => {
            sql.push_str(&format!(
                " AND epoch = {}",
                &params
                    .key_for("epoch")
                    .expect("epoch was added to the params list")
            ));
        }
        ValueStateRetrievalFlag::SpecificVersion(_) => {
            sql.push_str(&format!(
                " AND version = {}",
                &params
                    .key_for("version")
                    .expect("version was added to the params list")
            ));
        }
        ValueStateRetrievalFlag::MaxEpoch => {
            sql.push_str(" ORDER BY epoch DESC ");
        }
        ValueStateRetrievalFlag::MinEpoch => {
            sql.push_str(" ORDER BY epoch ASC ");
        }
        ValueStateRetrievalFlag::LeqEpoch(_) => {
            sql.push_str(&format!(
                r#"
                AND epoch <= {}
                ORDER BY epoch DESC
                "#,
                &params
                    .key_for("epoch")
                    .expect("epoch was added to the params list")
            ));
        }
    }

    QueryStatement::new(sql, params, from_row)
}

pub fn get_versions_by_flag(
    temp_table_name: &str,
    flag: ValueStateRetrievalFlag,
) -> QueryStatement<LabelVersion> {
    let mut params = SqlParams::new();

    let (filter, epoch_col) = match flag {
        ValueStateRetrievalFlag::SpecificVersion(version) => {
            params.add("version", Box::new(version as i64));
            (format!("WHERE tmp.version = {}", params.key_for("version").expect("version was added to the params list")), "tmp.epoch")
        }
        ValueStateRetrievalFlag::SpecificEpoch(epoch) => {
            params.add("epoch", Box::new(epoch as i64));
            (format!("WHERE tmp.epoch = {}", params.key_for("epoch").expect("epoch was added to the params list")), "tmp.epoch")
        }
        ValueStateRetrievalFlag::LeqEpoch(epoch) => {
            params.add("epoch", Box::new(epoch as i64));
            (format!("WHERE tmp.epoch <= {}", params.key_for("epoch").expect("epoch was added to the params list")), "MAX(tmp.epoch)")
        }
        ValueStateRetrievalFlag::MaxEpoch => ("".to_string(), "MAX(tmp.epoch)"),
        ValueStateRetrievalFlag::MinEpoch => ("".to_string(), "MIN(tmp.epoch)"),
    };

    let sql = format!(
        r#"
        SELECT t.raw_label, t.version, t.data
        FROM {TABLE_VALUES} t
        INNER JOIN (
            SELECT tmp.raw_label as raw_label, {} as epoch
            FROM {TABLE_VALUES} tmp
            INNER JOIN {temp_table_name} s ON s.raw_label = tmp.raw_label
            {}
            GROUP BY tmp.raw_label
        ) epochs on epochs.raw_label = t.raw_label AND epochs.epoch = t.epoch
        "#,
        epoch_col,
        filter,
    );

    QueryStatement::new(sql, params, version_from_row)
}

pub(crate) fn from_row(row: &Row) -> Result<ValueState, StorageError> {
    let raw_label: &[u8] = row
        .get("raw_label")
        .ok_or_else(|| StorageError::Other("raw_label is NULL or missing".to_string()))?;
    let epoch: i64 = row
        .get("epoch")
        .ok_or_else(|| StorageError::Other("epoch is NULL or missing".to_string()))?;
    let version: i64 = row
        .get("version")
        .ok_or_else(|| StorageError::Other("version is NULL or missing".to_string()))?;
    let node_label_val: &[u8] = row
        .get("node_label_val")
        .ok_or_else(|| StorageError::Other("node_label_val is NULL or missing".to_string()))?;
    let node_label_len: i64 = row
        .get("node_label_len")
        .ok_or_else(|| StorageError::Other("node_label_len is NULL or missing".to_string()))?;
    let data: &[u8] = row
        .get("data")
        .ok_or_else(|| StorageError::Other("data is NULL or missing".to_string()))?;

    let state = DbRecord::build_user_state(
        raw_label.to_vec(),
        data.to_vec(),
        version as u64,
        node_label_len as u32,
        node_label_val
            .to_vec()
            .try_into()
            .map_err(|_| StorageError::Other("node_label_val has incorrect length".to_string()))?,
        epoch as u64,
    );
    Ok(state)
}

pub(crate) struct LabelVersion {
    pub label: AkdLabel,
    pub version: u64,
    pub data: AkdValue,
}

fn version_from_row(row: &Row) -> Result<LabelVersion, StorageError> {
    let raw_label: &[u8] = row
        .get("raw_label")
        .ok_or_else(|| StorageError::Other("raw_label is NULL or missing".to_string()))?;
    let version: i64 = row
        .get("version")
        .ok_or_else(|| StorageError::Other("version is NULL or missing".to_string()))?;
    let data: &[u8] = row
        .get("data")
        .ok_or_else(|| StorageError::Other("data is NULL or missing".to_string()))?;

    Ok(LabelVersion {
        label: AkdLabel(raw_label.to_vec()),
        version: version as u64,
        data: AkdValue(data.to_vec()),
    })
}
