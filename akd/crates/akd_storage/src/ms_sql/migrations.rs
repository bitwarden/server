use ms_database::{load_migrations, Migration};

// Table names as constants
pub const TABLE_AZKS: &str = "akd_azks";
pub const TABLE_HISTORY_TREE_NODES: &str = "akd_history_tree_nodes";
pub const TABLE_VALUES: &str = "akd_values";
pub const TABLE_VRF_KEYS: &str = "akd_vrf_keys";
pub const TABLE_MIGRATIONS: &str = ms_database::TABLE_MIGRATIONS;

pub(crate) const MIGRATIONS: &[Migration] = load_migrations!("migrations/ms_sql");
