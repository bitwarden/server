use macros::migration;
use ms_database::Migration;

const LONG_MIGRATION: Migration = migration!("tests/test_panic_migrations/20250930_03_really_long_name_that_exceeds_the_50_character_limit");
const NO_UP_MIGRATION: Migration = migration!("tests/test_panic_migrations/20250930_04_no_up");

fn main() {}
