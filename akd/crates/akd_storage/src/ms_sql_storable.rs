use akd::{
    errors::StorageError,
    storage::{
        types::{DbRecord, StorageType, ValueState},
        Storable,
    },
    tree_node::TreeNodeWithPreviousValue,
    NodeLabel,
};
use ms_database::{ColumnData, IntoRow, Row, ToSql, TokenRow};
use tracing::debug;

use crate::{migrations::{
    TABLE_AZKS, TABLE_HISTORY_TREE_NODES, TABLE_VALUES
}, temp_table::TempTable};
use crate::sql_params::SqlParams;

const SELECT_AZKS_DATA: &'static [&str] = &["epoch", "num_nodes"];
const SELECT_HISTORY_TREE_NODE_DATA: &'static [&str] = &[
    "label_len",
    "label_val",
    "last_epoch",
    "least_descendant_ep",
    "parent_label_len",
    "parent_label_val",
    "node_type",
    "left_child_len",
    "left_child_label_val",
    "right_child_len",
    "right_child_label_val",
    "hash",
    "p_last_epoch",
    "p_least_descendant_ep",
    "p_parent_label_len",
    "p_parent_label_val",
    "p_node_type",
    "p_left_child_len",
    "p_left_child_label_val",
    "p_right_child_len",
    "p_right_child_label_val",
    "p_hash",
];
const SELECT_LABEL_DATA: &'static [&str] = &[
    "raw_label",
    "epoch",
    "version",
    "node_label_val",
    "node_label_len",
    "data",
];

pub(crate) struct Statement {
    sql: String,
    params: SqlParams,
}

impl Statement {
    pub fn new(sql: String, params: SqlParams) -> Self {
        debug!(sql, ?params, "Constructed SQL Statement");
        Self { sql, params }
    }

    pub fn sql(&self) -> &str {
        &self.sql
    }

    pub fn params(&self) -> Vec<&dyn ToSql> {
        self.params.values()
    }
}

pub(crate) struct QueryStatement<Out> {
    sql: String,
    params: SqlParams,
    parser: fn(&Row) -> Result<Out, StorageError>,
}

impl<Out> QueryStatement<Out> {
    pub fn new(sql: String, params: SqlParams, parser: fn(&Row) -> Result<Out, StorageError>) -> Self {
        Self { sql, params, parser }
    }

    pub fn sql(&self) -> &str {
        &self.sql
    }

    pub fn params(&self) -> Vec<&dyn ToSql> {
        self.params.values()
    }

    pub fn parse(&self, row: &Row) -> Result<Out, StorageError> {
        (self.parser)(row)
    }
}

pub(crate) trait MsSqlStorable {
    fn set_statement(&self) -> Result<Statement, StorageError>;

    fn set_batch_statement(storage_type: &StorageType) -> String;

    fn get_statement<St: Storable>(key: &St::StorageKey) -> Result<Statement, StorageError>;

    fn get_batch_temp_table_rows<St: Storable>(
        key: &[St::StorageKey],
    ) -> Result<Vec<TokenRow>, StorageError>;

    fn get_batch_statement<St: Storable>() -> String;

    fn from_row<St: Storable>(row: &Row) -> Result<Self, StorageError>
    where
        Self: Sized;

    fn into_row(&self) -> Result<TokenRow, StorageError>;
}

impl MsSqlStorable for DbRecord {
    fn set_statement(&self) -> Result<Statement, StorageError> {
        let record_type = match &self {
            DbRecord::Azks(_) => "Azks",
            DbRecord::TreeNode(_) => "TreeNode",
            DbRecord::ValueState(_) => "ValueState",
        };
        debug!(record_type, "Generating set statement");
        match &self {
            DbRecord::Azks(azks) => {
                debug!(epoch = azks.latest_epoch, num_nodes = azks.num_nodes, "Building AZKS set statement");
                let mut params = SqlParams::new();
                params.add("key", Box::new(1u8)); // constant key
                                                  // TODO: Fixup as conversions
                params.add("epoch", Box::new(azks.latest_epoch as i64));
                params.add("num_nodes", Box::new(azks.num_nodes as i64));

                let sql = format!(
                    r#"
                    MERGE INTO dbo.{TABLE_AZKS} AS t
                    USING (SELECT {}) AS source
                    ON t.[key] = source.[key]
                    WHEN MATCHED THEN
                        UPDATE SET {}
                    WHEN NOT MATCHED THEN
                        INSERT ({})
                        VALUES ({});
                "#,
                    params.keys_as_columns().join(", "),
                    params.set_columns_equal_except("t.", "source.", vec!["key"]).join(", "),
                    params.columns().join(", "),
                    params.keys().join(", ")
                );

                Ok(Statement::new(sql, params))
            }
            DbRecord::TreeNode(node) => {
                let mut params = SqlParams::new();
                params.add("label_len", Box::new(node.label.label_len as i32));
                params.add("label_val", Box::new(node.label.label_val.to_vec()));
                // Latest node values
                params.add("last_epoch", Box::new(node.latest_node.last_epoch as i64));
                params.add(
                    "least_descendant_ep",
                    Box::new(node.latest_node.min_descendant_epoch as i64),
                );
                params.add(
                    "parent_label_len",
                    Box::new(node.latest_node.parent.label_len as i32),
                );
                params.add(
                    "parent_label_val",
                    Box::new(node.latest_node.parent.label_val.to_vec()),
                );
                params.add("node_type", Box::new(node.latest_node.node_type as i16));
                params.add(
                    "left_child_len",
                    Box::new(node.latest_node.left_child.map(|lc| lc.label_len as i32)),
                );
                params.add(
                    "left_child_val",
                    Box::new(node.latest_node.left_child.map(|lc| lc.label_val.to_vec())),
                );
                params.add(
                    "right_child_len",
                    Box::new(node.latest_node.right_child.map(|rc| rc.label_len as i32)),
                );
                params.add(
                    "right_child_val",
                    Box::new(node.latest_node.right_child.map(|rc| rc.label_val.to_vec())),
                );
                params.add("[hash]", Box::new(node.latest_node.hash.0.to_vec()));
                // Previous node values
                params.add(
                    "p_last_epoch",
                    Box::new(node.previous_node.clone().map(|p| p.last_epoch as i64)),
                );
                params.add(
                    "p_least_descendant_ep",
                    Box::new(
                        node.previous_node
                            .clone()
                            .map(|p| p.min_descendant_epoch as i64),
                    ),
                );
                params.add(
                    "p_parent_label_len",
                    Box::new(node.previous_node.clone().map(|p| p.label.label_len as i32)),
                );
                params.add(
                    "p_parent_label_val",
                    Box::new(
                        node.previous_node
                            .clone()
                            .map(|p| p.label.label_val.to_vec()),
                    ),
                );
                params.add(
                    "p_node_type",
                    Box::new(node.previous_node.clone().map(|p| p.node_type as i16)),
                );
                params.add(
                    "p_left_child_len",
                    Box::new(
                        node.previous_node
                            .clone()
                            .and_then(|p| p.left_child.map(|lc| lc.label_len as i32)),
                    ),
                );
                params.add(
                    "p_left_child_val",
                    Box::new(
                        node.previous_node
                            .clone()
                            .and_then(|p| p.left_child.map(|lc| lc.label_val.to_vec())),
                    ),
                );
                params.add(
                    "p_right_child_len",
                    Box::new(
                        node.previous_node
                            .clone()
                            .and_then(|p| p.right_child.map(|rc| rc.label_len as i32)),
                    ),
                );
                params.add(
                    "p_right_child_val",
                    Box::new(
                        node.previous_node
                            .clone()
                            .and_then(|p| p.right_child.map(|rc| rc.label_val.to_vec())),
                    ),
                );
                params.add(
                    "p_hash",
                    Box::new(node.previous_node.clone().map(|p| p.hash.0.to_vec())),
                );

                let sql = format!(
                    r#"
                    MERGE INTO dbo.{TABLE_HISTORY_TREE_NODES} AS t
                    USING (SELECT {}) AS source
                    ON t.label_len = source.label_len AND t.label_val = source.label_val
                    WHEN MATCHED THEN
                        UPDATE SET {}
                    WHEN NOT MATCHED THEN
                        INSERT ({})
                        VALUES ({});
                    "#,
                    params.keys_as_columns().join(", "),
                    params
                        .set_columns_equal_except(
                            "t.",
                            "source.",
                            vec!["label_len", "label_val"]
                        )
                        .join(", "),
                    params.columns().join(", "),
                    params.keys().join(", "),
                );

                Ok(Statement::new(sql, params))
            }
            DbRecord::ValueState(state) => {
                let mut params = SqlParams::new();
                params.add("raw_label", Box::new(state.get_id().0.clone()));
                // TODO: Fixup as conversions
                params.add("epoch", Box::new(state.epoch as i64));
                params.add("[version]", Box::new(state.version as i64));
                params.add("node_label_val", Box::new(state.label.label_val.to_vec()));
                params.add("node_label_len", Box::new(state.label.label_len as i64));
                params.add("[data]", Box::new(state.value.0.clone()));

                // Note: raw_label & epoch are combined the primary key, so these are always new
                let sql = format!(
                    r#"
                    INSERT INTO dbo.{TABLE_VALUES} ({})
                    VALUES ({});
                    "#,
                    params.columns().join(", "),
                    params.keys().join(", "),
                );
                Ok(Statement::new(sql, params))
            }
        }
    }

    fn set_batch_statement(storage_type: &StorageType) -> String {
        match storage_type {
            StorageType::Azks => format!(
                r#"
                MERGE INTO dbo.{TABLE_AZKS} AS t
                USING {} AS source
                ON t.[key] = source.[key]
                WHEN MATCHED THEN
                    UPDATE SET 
                        t.[epoch] = source.[epoch],
                        t.[num_nodes] = source.[num_nodes]
                WHEN NOT MATCHED THEN
                    INSERT ([key], [epoch], [num_nodes])
                    VALUES (source.[key], source.[epoch], source.[num_nodes]);
                "#,
                TempTable::Azks.to_string()
            ),
            StorageType::TreeNode => format!(
                r#"
                MERGE INTO dbo.{TABLE_HISTORY_TREE_NODES} AS t
                USING {} AS source
                ON t.label_len = source.label_len AND t.label_val = source.label_val
                WHEN MATCHED THEN
                    UPDATE SET 
                        t.last_epoch = source.last_epoch,
                        t.least_descendant_ep = source.least_descendant_ep,
                        t.parent_label_len = source.parent_label_len,
                        t.parent_label_val = source.parent_label_val,
                        t.node_type = source.node_type,
                        t.left_child_len = source.left_child_len,
                        t.left_child_label_val = source.left_child_label_val,
                        t.right_child_len = source.right_child_len,
                        t.right_child_label_val = source.right_child_label_val,
                        t.hash = source.hash,
                        t.p_last_epoch = source.p_last_epoch,
                        t.p_least_descendant_ep = source.p_least_descendant_ep,
                        t.p_parent_label_len = source.p_parent_label_len,
                        t.p_parent_label_val = source.p_parent_label_val,
                        t.p_node_type = source.p_node_type,
                        t.p_left_child_len = source.p_left_child_len,
                        t.p_left_child_label_val = source.p_left_child_label_val,
                        t.p_right_child_len = source.p_right_child_len,
                        t.p_right_child_label_val = source.p_right_child_label_val,
                        t.p_hash = source.p_hash
                WHEN NOT MATCHED THEN
                    INSERT (
                        label_len
                        , label_val
                        , last_epoch
                        , least_descendant_ep
                        , parent_label_len
                        , parent_label_val
                        , node_type
                        , left_child_len
                        , left_child_label_val
                        , right_child_len
                        , right_child_label_val
                        , hash
                        , p_last_epoch
                        , p_least_descendant_ep
                        , p_parent_label_len
                        , p_parent_label_val
                        , p_node_type
                        , p_left_child_len
                        , p_left_child_label_val
                        , p_right_child_len
                        , p_right_child_label_val
                        , p_hash
                    )
                    VALUES (
                        source.label_len
                        , source.label_val
                        , source.last_epoch
                        , source.least_descendant_ep
                        , source.parent_label_len
                        , source.parent_label_val
                        , source.node_type
                        , source.left_child_len
                        , source.left_child_label_val
                        , source.right_child_len
                        , source.right_child_label_val
                        , source.hash
                        , source.p_last_epoch
                        , source.p_least_descendant_ep
                        , source.p_parent_label_len
                        , source.p_parent_label_val
                        , source.p_node_type
                        , source.p_left_child_len
                        , source.p_left_child_label_val
                        , source.p_right_child_len
                        , source.p_right_child_label_val
                        , source.p_hash
                    );
                "#,
                TempTable::HistoryTreeNodes.to_string()
            ),
            StorageType::ValueState => format!(
                r#"
                    MERGE INTO dbo.{TABLE_VALUES} AS t
                    USING {} AS source
                    ON t.raw_label = source.raw_label AND t.epoch = source.epoch
                    WHEN MATCHED THEN
                        UPDATE SET 
                            t.[version] = source.[version],
                            t.node_label_val = source.node_label_val,
                            t.node_label_len = source.node_label_len,
                            t.[data] = source.[data]
                    WHEN NOT MATCHED THEN
                        INSERT (raw_label, epoch, [version], node_label_val, node_label_len, [data])
                        VALUES (source.raw_label, source.epoch, source.[version], source.node_label_val, source.node_label_len, source.[data]);
                    "#,
                TempTable::Values.to_string()
            ),
        }
    }

    fn get_statement<St: Storable>(key: &St::StorageKey) -> Result<Statement, StorageError> {
        let mut params = SqlParams::new();
        let sql = match St::data_type() {
            StorageType::Azks => {
                format!(
                    r#"
                    SELECT TOP(1) {} from dbo.{};
                    "#,
                    SELECT_AZKS_DATA.join(", "),
                    TABLE_AZKS
                )
            }
            StorageType::TreeNode => {
                let bin = St::get_full_binary_key_id(key);
                // These are constructed from a safe key, they should never fail
                let key = TreeNodeWithPreviousValue::key_from_full_binary(&bin)
                    .expect("Failed to decode key"); // TODO: should this be an error?

                params.add("label_len", Box::new(key.0.label_len as i32));
                params.add("label_val", Box::new(key.0.label_val.to_vec()));

                format!(
                    r#"
                    SELECT {} from dbo.{TABLE_HISTORY_TREE_NODES} WHERE [label_len] = {} AND [label_val] = {};
                    "#,
                    SELECT_HISTORY_TREE_NODE_DATA.join(", "),
                    params.key_for("label_len").expect("key present"),
                    params.key_for("label_val").expect("key present"),
                )
            }
            StorageType::ValueState => {
                let bin = St::get_full_binary_key_id(key);
                // These are constructed from a safe key, they should never fail
                let key = ValueState::key_from_full_binary(&bin).expect("Failed to decode key"); // TODO: should this be an error?

                params.add("raw_label", Box::new(key.0.clone()));
                params.add("epoch", Box::new(key.1 as i64));
                format!(
                    r#"
                    SELECT {} from dbo.{TABLE_VALUES} WHERE [raw_label] = {} AND [epoch] = {};
                    "#,
                    SELECT_LABEL_DATA.join(", "),
                    params.key_for("raw_label").expect("key present"),
                    params.key_for("epoch").expect("key present"),
                )
            }
        };

        Ok(Statement::new(sql, params))
    }

    fn get_batch_temp_table_rows<St: Storable>(
        key: &[St::StorageKey],
    ) -> Result<Vec<TokenRow>, StorageError> {
        match St::data_type() {
            StorageType::Azks => Err(StorageError::Other(
                "Batch temp table rows not supported for Azks".to_string(),
            )),
            StorageType::TreeNode => {
                let mut rows = Vec::new();
                for k in key {
                    let bin = St::get_full_binary_key_id(k);
                    // These are constructed from a safe key, they should never fail
                    let key = TreeNodeWithPreviousValue::key_from_full_binary(&bin)
                        .expect("Failed to decode key");

                    let row = (key.0.label_len as i32, key.0.label_val.to_vec()).into_row();
                    rows.push(row);
                }
                Ok(rows)
            }
            StorageType::ValueState => {
                let mut rows = Vec::new();
                for k in key {
                    let bin = St::get_full_binary_key_id(k);
                    // These are constructed from a safe key, they should never fail
                    let key = ValueState::key_from_full_binary(&bin).expect("Failed to decode key"); // TODO: should this be an error?

                    let row = (key.0.clone(), key.1 as i64).into_row();
                    rows.push(row);
                }
                Ok(rows)
            }
        }
    }

    fn get_batch_statement<St: Storable>() -> String {
        // Note: any changes to these columns need to be reflected in from_row below
        match St::data_type() {
            StorageType::Azks => panic!("Batch get not supported for Azks"),
            StorageType::TreeNode => format!(
                r#"
                SELECT {} 
                FROM dbo.{TABLE_HISTORY_TREE_NODES} h
                INNER JOIN {} t
                    ON h.label_len = t.label_len
                    AND h.label_val = t.label_val;
                "#,
                SELECT_HISTORY_TREE_NODE_DATA
                    .iter()
                    .map(|s| format!("h.{s}"))
                    .collect::<Vec<_>>()
                    .join(", "),
                TempTable::for_ids::<St>().to_string()
            ),
            StorageType::ValueState => format!(
                r#"
                SELECT {} 
                FROM dbo.{TABLE_VALUES} v
                INNER JOIN {} t 
                    ON v.raw_label = t.raw_label 
                    AND v.epoch = t.epoch;
                "#,
                SELECT_LABEL_DATA
                    .iter()
                    .map(|s| format!("v.{s}"))
                    .collect::<Vec<_>>()
                    .join(", "),
                TempTable::for_ids::<St>().to_string()
            ),
        }
    }

    fn from_row<St: Storable>(row: &Row) -> Result<Self, StorageError>
    where
        Self: Sized,
    {
        match St::data_type() {
            // TODO: check this
            StorageType::Azks => {
                let epoch: i64 = row
                    .get("epoch")
                    .ok_or_else(|| StorageError::Other("epoch is NULL or missing".to_string()))?;
                let num_nodes: i64 = row.get("num_nodes").ok_or_else(|| {
                    StorageError::Other("num_nodes is NULL or missing".to_string())
                })?;

                let azks = DbRecord::build_azks(epoch as u64, num_nodes as u64);
                Ok(DbRecord::Azks(azks))
            }
            StorageType::TreeNode => {
                let label_len: i32 = row.get("label_len").ok_or_else(|| {
                    StorageError::Other("label_len is NULL or missing".to_string())
                })?;
                let label_val: &[u8] = row.get("label_val").ok_or_else(|| {
                    StorageError::Other("label_val is NULL or missing".to_string())
                })?;
                let last_epoch: i64 = row.get("last_epoch").ok_or_else(|| {
                    StorageError::Other("last_epoch is NULL or missing".to_string())
                })?;
                let least_descendant_ep: i64 = row.get("least_descendant_ep").ok_or_else(|| {
                    StorageError::Other("least_descendant_ep is NULL or missing".to_string())
                })?;
                let parent_label_len: i32 = row.get("parent_label_len").ok_or_else(|| {
                    StorageError::Other("parent_label_len is NULL or missing".to_string())
                })?;
                let parent_label_val: &[u8] = row.get("parent_label_val").ok_or_else(|| {
                    StorageError::Other("parent_label_val is NULL or missing".to_string())
                })?;
                let node_type: i16 = row.get("node_type").ok_or_else(|| {
                    StorageError::Other("node_type is NULL or missing".to_string())
                })?;
                let left_child_len: Option<i32> = row.get("left_child_len");
                let left_child_label_val: Option<&[u8]> = row.get("left_child_label_val");
                let right_child_len: Option<i32> = row.get("right_child_len");
                let right_child_label_val: Option<&[u8]> = row.get("right_child_len");
                let hash: &[u8] = row
                    .get("hash")
                    .ok_or_else(|| StorageError::Other("hash is NULL or missing".to_string()))?;
                let p_last_epoch: Option<i64> = row.get("p_last_epoch");
                let p_least_descendant_ep: Option<i64> = row.get("p_least_descendant_ep");
                let p_parent_label_len: Option<i32> = row.get("p_parent_label_len");
                let p_parent_label_val: Option<&[u8]> = row.get("p_parent_label_val");
                let p_node_type: Option<i16> = row.get("p_node_type");
                let p_left_child_len: Option<i32> = row.get("p_left_child_len");
                let p_left_child_label_val: Option<&[u8]> = row.get("p_left_child_label_val");
                let p_right_child_len: Option<i32> = row.get("p_right_child_len");
                let p_right_child_label_val: Option<&[u8]> = row.get("p_right_child_label_val");
                let p_hash: Option<&[u8]> = row.get("p_hash");

                // Make child nodes
                fn optional_child_label(
                    child_val: Option<&[u8]>,
                    child_len: Option<i32>,
                ) -> Result<Option<NodeLabel>, StorageError> {
                    match (child_val, child_len) {
                        (Some(val), Some(len)) => {
                            let val_vec: Vec<u8> = val.to_vec().try_into().map_err(|_| {
                                StorageError::Other("child_val has incorrect length".to_string())
                            })?;
                            Ok(Some(NodeLabel::new(
                                val_vec.try_into().map_err(|_| {
                                    StorageError::Other(
                                        "child_val has incorrect length".to_string(),
                                    )
                                })?,
                                len as u32,
                            )))
                        }
                        _ => Ok(None),
                    }
                }
                let left_child = optional_child_label(left_child_label_val, left_child_len)?;
                let right_child = optional_child_label(right_child_label_val, right_child_len)?;
                let p_left_child = optional_child_label(p_left_child_label_val, p_left_child_len)?;
                let p_right_child =
                    optional_child_label(p_right_child_label_val, p_right_child_len)?;

                let massaged_p_parent_label_val: Option<[u8; 32]> = match p_parent_label_val {
                    Some(v) => Some(v.to_vec().try_into().map_err(|_| {
                        StorageError::Other("p_parent_label_val has incorrect length".to_string())
                    })?),
                    None => None,
                };
                let massaged_hash: akd::Digest = akd::hash::try_parse_digest(&hash.to_vec())
                    .map_err(|_| StorageError::Other("hash has incorrect length".to_string()))?;
                let massaged_p_hash: Option<akd::Digest> = match p_hash {
                    Some(v) => Some(akd::hash::try_parse_digest(&v.to_vec()).map_err(|_| {
                        StorageError::Other("p_hash has incorrect length".to_string())
                    })?),
                    None => None,
                };

                let node = DbRecord::build_tree_node_with_previous_value(
                    label_val.try_into().map_err(|_| {
                        StorageError::Other("label_val has incorrect length".to_string())
                    })?,
                    label_len as u32,
                    last_epoch as u64,
                    least_descendant_ep as u64,
                    parent_label_val.try_into().map_err(|_| {
                        StorageError::Other("parent_label_val has incorrect length".to_string())
                    })?,
                    parent_label_len as u32,
                    node_type as u8,
                    left_child,
                    right_child,
                    massaged_hash,
                    p_last_epoch.map(|v| v as u64),
                    p_least_descendant_ep.map(|v| v as u64),
                    massaged_p_parent_label_val,
                    p_parent_label_len.map(|v| v as u32),
                    p_node_type.map(|v| v as u8),
                    p_left_child,
                    p_right_child,
                    massaged_p_hash,
                );

                Ok(DbRecord::TreeNode(node))
            }
            StorageType::ValueState => Ok(DbRecord::ValueState(crate::tables::values::from_row(row)?)),
        }
    }

    fn into_row(&self) -> Result<TokenRow, StorageError> {
        match &self {
            DbRecord::Azks(azks) => {
                let row = (
                    1u8, // constant key
                    azks.latest_epoch as i64,
                    azks.num_nodes as i64,
                )
                    .into_row();
                Ok(row)
            }
            DbRecord::TreeNode(node) => {
                let mut row = TokenRow::new();
                row.push(ColumnData::I32(Some(node.label.label_len as i32)));
                row.push(ColumnData::Binary(Some(
                    node.label.label_val.to_vec().into(),
                )));
                // Latest node values
                row.push(ColumnData::I64(Some(node.latest_node.last_epoch as i64)));
                row.push(ColumnData::I64(Some(
                    node.latest_node.min_descendant_epoch as i64,
                )));
                row.push(ColumnData::I32(Some(
                    node.latest_node.parent.label_len as i32,
                )));
                row.push(ColumnData::Binary(Some(
                    node.latest_node.parent.label_val.to_vec().into(),
                )));
                row.push(ColumnData::I16(Some(node.latest_node.node_type as i16)));
                match &node.latest_node.left_child {
                    Some(lc) => {
                        row.push(ColumnData::I32(Some(lc.label_len as i32)));
                        row.push(ColumnData::Binary(Some(lc.label_val.to_vec().into())));
                    }
                    None => {
                        row.push(ColumnData::I32(None));
                        row.push(ColumnData::Binary(None));
                    }
                }
                match &node.latest_node.right_child {
                    Some(rc) => {
                        row.push(ColumnData::I32(Some(rc.label_len as i32)));
                        row.push(ColumnData::Binary(Some(rc.label_val.to_vec().into())));
                    }
                    None => {
                        row.push(ColumnData::I32(None));
                        row.push(ColumnData::Binary(None));
                    }
                }
                row.push(ColumnData::Binary(Some(
                    node.latest_node.hash.0.to_vec().into(),
                )));
                // Previous node values
                match &node.previous_node {
                    Some(p) => {
                        row.push(ColumnData::I64(Some(p.last_epoch as i64)));
                        row.push(ColumnData::I64(Some(p.min_descendant_epoch as i64)));
                        row.push(ColumnData::I32(Some(p.label.label_len as i32)));
                        row.push(ColumnData::Binary(Some(p.label.label_val.to_vec().into())));
                        row.push(ColumnData::I16(Some(p.node_type as i16)));
                        match &p.left_child {
                            Some(lc) => {
                                row.push(ColumnData::I32(Some(lc.label_len as i32)));
                                row.push(ColumnData::Binary(Some(lc.label_val.to_vec().into())));
                            }
                            None => {
                                row.push(ColumnData::I32(None));
                                row.push(ColumnData::Binary(None));
                            }
                        }
                        match &p.right_child {
                            Some(rc) => {
                                row.push(ColumnData::I32(Some(rc.label_len as i32)));
                                row.push(ColumnData::Binary(Some(rc.label_val.to_vec().into())));
                            }
                            None => {
                                row.push(ColumnData::I32(None));
                                row.push(ColumnData::Binary(None));
                            }
                        }
                        row.push(ColumnData::Binary(Some(p.hash.0.to_vec().into())));
                    }
                    None => {
                        // Node Values
                        row.push(ColumnData::I64(None));
                        row.push(ColumnData::I64(None));
                        row.push(ColumnData::I32(None));
                        row.push(ColumnData::Binary(None));
                        row.push(ColumnData::I16(None));
                        // Left child
                        row.push(ColumnData::I32(None));
                        row.push(ColumnData::Binary(None));
                        // Right child
                        row.push(ColumnData::I32(None));
                        row.push(ColumnData::Binary(None));
                        // Hash
                        row.push(ColumnData::Binary(None));
                    }
                }
                Ok(row)
            }
            DbRecord::ValueState(state) => {
                let row = (
                    state.get_id().0.clone(),
                    state.epoch as i64,
                    state.version as i64,
                    state.label.label_val.to_vec(),
                    state.label.label_len as i32,
                    state.value.0.clone(),
                )
                    .into_row();
                Ok(row)
            }
        }
    }
}
