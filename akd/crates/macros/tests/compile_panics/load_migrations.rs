use macros::load_migrations;
use ms_database::Migration;

const MIGRATIONS: &[Migration] = load_migrations!("tests/test_panic_migrations");

fn main() {}
