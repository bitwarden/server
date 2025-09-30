use macros::load_migrations;
use ms_database::Migration;

const TEST_MIGRIATONS: &[Migration] = load_migrations!("tests/test_migrations");
const EXPECTED_SQL: &str = "SELECT 1;";

#[test]
fn migration_ordering() {
    let names: Vec<&str> = TEST_MIGRIATONS.iter().map(|m| m.name).collect();
    assert_eq!(names, vec!["20250930_01_test", "20250930_02_up_down",]);
}

#[test]
fn up_only_migration() {
    let migration = &TEST_MIGRIATONS[0];
    assert_eq!(migration.name, "20250930_01_test");
    assert_eq!(migration.up, EXPECTED_SQL);
    assert!(migration.down.is_none());
    assert!(migration.run_in_transaction);
}

#[test]
fn up_down_migration() {
    let migration = &TEST_MIGRIATONS[1];
    assert_eq!(migration.name, "20250930_02_up_down");
    assert_eq!(migration.up, EXPECTED_SQL);
    assert_eq!(migration.down, Some(EXPECTED_SQL));
    assert!(migration.run_in_transaction);
}

#[test]
fn long_name_migration() {
    let t = trybuild::TestCases::new();
    t.compile_fail("tests/compile_panics/load_migrations.rs");
}
