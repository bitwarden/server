use macros::{migration};
use ms_database::Migration;

const EXPECTED_SQL: &str = "SELECT 1;";

#[test]
fn up_only_migration() {
    let migration = migration!("tests/test_migrations/20250930_01_test");
    assert_eq!(migration.name, "20250930_01_test");
    assert_eq!(migration.up, EXPECTED_SQL);
    assert!(migration.down.is_none());
    assert!(migration.run_in_transaction);
}

#[test]
fn up_down_migration() {
    let migration = migration!("tests/test_migrations/20250930_02_up_down");
    assert_eq!(migration.name, "20250930_02_up_down");
    assert_eq!(migration.up, EXPECTED_SQL);
    assert_eq!(migration.down, Some(EXPECTED_SQL));
    assert!(migration.run_in_transaction);
}

#[test]
fn long_name_migration() {
    let t = trybuild::TestCases::new();
    t.compile_fail("tests/compile_panics/migration.rs");
}
