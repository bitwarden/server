use akd::{AkdLabel, AkdValue};
use ms_database::{IntoRow, TokenRow};
use tracing::{debug, error};
use uuid::Uuid;

use crate::{
    ms_sql::{
        migrations::TABLE_PUBLISH_QUEUE,
        sql_params::SqlParams,
        tables::akd_storable_for_ms_sql::{QueryStatement, Statement},
    },
    publish_queue::PublishQueueError,
};

pub fn enqueue_statement(label: AkdLabel, value: AkdValue) -> Statement {
    debug!("Building enqueue_statement for publish queue");
    let mut params = SqlParams::new();
    params.add("id", Box::new(uuid::Uuid::now_v7()));
    params.add("raw_label", Box::new(label.0));
    params.add("raw_value", Box::new(value.0));

    let sql = format!(
        r#"
        INSERT INTO {}
        (id, raw_label, raw_value)
        VALUES ({}, {}, {})"#,
        TABLE_PUBLISH_QUEUE,
        params
            .key_for("id")
            .expect("id was added to the params list"),
        params
            .key_for("raw_label")
            .expect("raw_label was added to the params list"),
        params
            .key_for("raw_value")
            .expect("raw_value was added to the params list"),
    );
    Statement::new(sql, params)
}

pub fn peek_statement(
    limit: isize,
) -> QueryStatement<(Uuid, (AkdLabel, AkdValue)), PublishQueueError> {
    debug!("Building peek_statement for publish queue");
    let sql = format!(
        r#"
        SELECT TOP {} id, raw_label, raw_value
        FROM {}
        ORDER BY id ASC"#,
        limit, TABLE_PUBLISH_QUEUE
    );
    QueryStatement::new(sql, SqlParams::new(), publish_queue_item_from_row)
}

pub fn peek_no_limit_statement() -> QueryStatement<(Uuid, (AkdLabel, AkdValue)), PublishQueueError>
{
    debug!("Building peek_statement with no limit for publish queue");
    let sql = format!(
        r#"
        SELECT id, raw_label, raw_value
        FROM {}
        ORDER BY id ASC"#,
        TABLE_PUBLISH_QUEUE
    );
    QueryStatement::new(sql, SqlParams::new(), publish_queue_item_from_row)
}

fn publish_queue_item_from_row(
    row: &ms_database::Row,
) -> Result<(Uuid, (AkdLabel, AkdValue)), PublishQueueError> {
    let id: uuid::Uuid = row.get("id").ok_or_else(|| {
        error!("id is NULL or missing in publish queue row");
        PublishQueueError
    })?;
    let raw_label: &[u8] = row.get("raw_label").ok_or_else(|| {
        error!("raw_label is NULL or missing in publish queue row");
        PublishQueueError
    })?;
    let raw_value: &[u8] = row.get("raw_value").ok_or_else(|| {
        error!("raw_value is NULL or missing in publish queue row");
        PublishQueueError
    })?;

    Ok((
        id,
        (AkdLabel(raw_label.to_vec()), AkdValue(raw_value.to_vec())),
    ))
}

pub fn bulk_delete_rows(ids: &'_ [Uuid]) -> Result<Vec<TokenRow<'_>>, PublishQueueError> {
    debug!("Building bulk_delete_rows for publish queue");
    let mut rows = Vec::new();
    for id in ids {
        let row = (id.clone()).into_row();
        rows.push(row);
    }
    Ok(rows)
}

pub fn bulk_delete_statement(temp_table_name: &str) -> Statement {
    debug!("Building bulk_delete_statement deleting ids in temp table from the publish queue");
    let sql = format!(
        r#"
        DELETE pq
        FROM {} pq
        INNER JOIN {} temp ON pq.id = temp.id
        "#,
        TABLE_PUBLISH_QUEUE, temp_table_name
    );
    Statement::new(sql, SqlParams::new())
}

pub fn label_pending_publish_statement(
    label: &AkdLabel,
) -> QueryStatement<bool, PublishQueueError> {
    debug!("Building label_pending_publish_statement for publish queue");
    let mut params = SqlParams::new();
    params.add("raw_label", Box::new(label.0.clone()));

    let sql = format!(
        r#"
        SELECT COUNT(1) AS label_count
        FROM {}
        WHERE raw_label = {}"#,
        TABLE_PUBLISH_QUEUE,
        params
            .key_for("raw_label")
            .expect("raw_label was added to the params list"),
    );
    QueryStatement::new(sql, params, |row: &ms_database::Row| {
        let count: i64 = row.get("label_count").ok_or_else(|| {
            error!("label_count is NULL or missing in publish queue row");
            PublishQueueError
        })?;
        Ok(count > 0)
    })
}
