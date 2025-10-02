use ms_database::{load_migrations, Migration};

// Table names as constants
pub(crate) const TABLE_AZKS: &str = "akd_azks";
pub(crate) const TABLE_HISTORY_TREE_NODES: &str = "akd_history_tree_nodes";
pub(crate) const TABLE_VALUES: &str = "akd_values";
pub(crate) const TEMP_IDS_TABLE: &str = "#akd_temp_ids";

pub(crate) const MIGRATIONS: &[Migration] = load_migrations!("migrations/ms_sql");
