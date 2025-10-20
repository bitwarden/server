use std::str::FromStr;

use akd::storage::{types::StorageType, Storable};

pub(crate) enum TempTable {
    Ids(StorageType),
    Azks,
    HistoryTreeNodes,
    Values,
}

impl TempTable {
    pub fn for_ids_for(storage_type: &StorageType) -> Self {
        TempTable::Ids(storage_type.clone())
    }

    pub fn for_ids<St: Storable>() -> Self {
        TempTable::Ids(St::data_type())
    }

    pub fn drop(&self) -> String {
        format!("DROP TABLE IF EXISTS {}", self.to_string())
    }

    pub fn can_create(&self) -> bool {
        match self {
            TempTable::Ids(storage_type) => match storage_type {
                StorageType::Azks => false,
                _ => true,
            },
            _ => true,
        }
    }

    pub fn create(&self) -> String {
        match self {
            TempTable::Azks => format!(
                r#"
                CREATE TABLE {} (
                    [key] SMALLINT NOT NULL PRIMARY KEY,
                    [epoch] BIGINT NOT NULL,
                    [num_nodes] BIGINT NOT NULL
                );
                "#,
                TEMP_AZKS_TABLE
            ),
            TempTable::HistoryTreeNodes => format!(
                r#"
                CREATE TABLE {} (
                    label_len INT NOT NULL CHECK (label_len >= 0),
                    label_val VARBINARY(32) NOT NULL,
                    last_epoch BIGINT NOT NULL CHECK (last_epoch >= 0),
                    least_descendant_ep BIGINT NOT NULL CHECK (least_descendant_ep >= 0),
                    parent_label_len INT NOT NULL CHECK (parent_label_len >= 0),
                    parent_label_val VARBINARY(32) NOT NULL,
                    node_type SMALLINT NOT NULL CHECK (node_type >= 0),
                    left_child_len INT NULL CHECK (left_child_len IS NULL OR left_child_len >= 0),
                    left_child_label_val VARBINARY(32) NULL,
                    right_child_len INT NULL CHECK (right_child_len IS NULL OR right_child_len >= 0),
                    right_child_label_val VARBINARY(32) NULL,
                    [hash] VARBINARY(32) NOT NULL,
                    p_last_epoch BIGINT NULL CHECK (p_last_epoch IS NULL OR p_last_epoch >= 0),
                    p_least_descendant_ep BIGINT NULL CHECK (p_least_descendant_ep IS NULL OR p_least_descendant_ep >= 0),
                    p_parent_label_len INT NULL CHECK (p_parent_label_len IS NULL OR p_parent_label_len >= 0),
                    p_parent_label_val VARBINARY(32) NULL,
                    p_node_type SMALLINT NULL CHECK (p_node_type IS NULL OR p_node_type >= 0),
                    p_left_child_len INT NULL CHECK (p_left_child_len IS NULL OR p_left_child_len >= 0),
                    p_left_child_label_val VARBINARY(32) NULL,
                    p_right_child_len INT NULL CHECK (p_right_child_len IS NULL OR p_right_child_len >= 0),
                    p_right_child_label_val VARBINARY(32) NULL,
                    p_hash VARBINARY(32) NULL,
                    PRIMARY KEY (label_len, label_val)
                );
                "#,
                TEMP_AZKS_TABLE
            ),
            TempTable::Values => format!(
                r#"
                CREATE TABLE {} (
                    raw_label VARBINARY(256) NOT NULL,
                    epoch BIGINT NOT NULL CHECK (epoch >= 0),
                    [version] BIGINT NOT NULL CHECK ([version] >= 0),
                    node_label_val VARBINARY(32) NOT NULL,
                    node_label_len INT NOT NULL CHECK (node_label_len >= 0),
                    [data] VARBINARY(2000) NULL,
                    PRIMARY KEY (raw_label, epoch)
                );
                "#,
                TEMP_VALUES_TABLE
            ),
            TempTable::Ids(storage_type) => match storage_type {
                StorageType::Azks => panic!("TempTable::Ids should not be created for Azks"),
                StorageType::TreeNode => format!(
                    r#"
                    CREATE TABLE {} (
                        label_len INT NOT NULL,
                        label_val VARBINARY(32) NOT NULL,
                        PRIMARY KEY (label_len, label_val)
                    );
                    "#,
                    TEMP_IDS_TABLE
                ),
                StorageType::ValueState => format!(
                    r#"
                    CREATE TABLE {} (
                        raw_label VARBINARY(256) NOT NULL,
                        epoch BIGINT NOT NULL,
                        PRIMARY KEY (raw_label, epoch)
                    );
                    "#,
                    TEMP_IDS_TABLE
                ),
            }
        }
    }
}

impl ToString for TempTable {
    fn to_string(&self) -> String {
        match self {
            TempTable::Ids(_) => TEMP_IDS_TABLE.to_string(),
            TempTable::Azks => TEMP_AZKS_TABLE.to_string(),
            TempTable::HistoryTreeNodes => TEMP_HISTORY_TREE_NODES_TABLE.to_string(),
            TempTable::Values => TEMP_VALUES_TABLE.to_string(),
        }
    }
}

impl From<StorageType> for TempTable {
    fn from(storage_type: StorageType) -> Self {
        match storage_type {
            StorageType::Azks => TempTable::Azks,
            StorageType::TreeNode => TempTable::HistoryTreeNodes,
            StorageType::ValueState => TempTable::Values,
        }
    }
}

pub(crate) const TEMP_IDS_TABLE: &str = "#akd_temp_ids";
pub(crate) const TEMP_AZKS_TABLE: &str = "#akd_temp_azks";
pub(crate) const TEMP_HISTORY_TREE_NODES_TABLE: &str = "#akd_temp_history_tree_nodes";
pub(crate) const TEMP_VALUES_TABLE: &str = "#akd_temp_values";
