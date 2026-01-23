use chrono::{DateTime, Utc};
use std::sync::Arc;
use tokio::sync::RwLock;

/// Tracks epoch publishes and provides prediction based on expected duration
#[derive(Debug, Clone)]
pub(crate) struct EpochTracker {
    inner: Arc<RwLock<EpochTrackerInner>>,
}

#[derive(Debug)]
struct EpochTrackerInner {
    last_publish_time: Option<DateTime<Utc>>,
    expected_epoch_duration_ms: u64,
}

impl EpochTracker {
    pub(crate) fn new(expected_epoch_duration_ms: u64) -> Self {
        Self {
            inner: Arc::new(RwLock::new(EpochTrackerInner {
                last_publish_time: None,
                expected_epoch_duration_ms,
            })),
        }
    }

    /// Record a new epoch publish
    pub(crate) async fn record_publish(&self, published_at: DateTime<Utc>) {
        let mut inner = self.inner.write().await;
        inner.last_publish_time = Some(published_at);
    }

    /// Predict the next epoch publish time using modulus calculation
    /// Returns (seconds_until_next, next_epoch_datetime) or None if no publish has been recorded yet
    pub(crate) async fn predict_next_epoch(
        &self,
        now: DateTime<Utc>,
    ) -> Option<(f64, DateTime<Utc>)> {
        let inner = self.inner.read().await;
        let last_publish = inner.last_publish_time?;

        // Calculate time since last publish
        let duration_since_publish = now - last_publish;
        let ms_since_publish = duration_since_publish.num_milliseconds();

        // Use modulus to find time until next epoch
        let ms_until_next = inner.expected_epoch_duration_ms as i64
            - (ms_since_publish % inner.expected_epoch_duration_ms as i64);

        // Calculate predicted next epoch time
        let next_epoch_time = now + chrono::Duration::milliseconds(ms_until_next);

        // Convert to seconds with tenths precision
        let seconds_until = ms_until_next as f64 / 1000.0;
        let rounded = (seconds_until * 10.0).ceil() / 10.0;

        Some((rounded, next_epoch_time))
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_tracker_no_prediction_before_publish() {
        let tracker = EpochTracker::new(30000);
        let now = Utc::now();
        assert!(tracker.predict_next_epoch(now).await.is_none());
    }

    #[tokio::test]
    async fn test_tracker_predicts_after_publish() {
        let tracker = EpochTracker::new(30000); // 30 second epochs
        let now = Utc::now();
        let publish_time = now - chrono::Duration::seconds(10); // 10 seconds ago

        tracker.record_publish(publish_time).await;

        let prediction = tracker.predict_next_epoch(now).await;
        assert!(prediction.is_some());

        let (seconds_until, next_time) = prediction.unwrap();

        // Should predict ~20 seconds until next (30 - 10)
        assert!((seconds_until - 20.0).abs() < 0.2);

        // Next epoch should be approximately 20 seconds from now
        let expected = now + chrono::Duration::seconds(20);
        let diff = (next_time - expected).num_seconds().abs();
        assert!(diff < 1);
    }

    #[tokio::test]
    async fn test_tracker_handles_epoch_skip() {
        let tracker = EpochTracker::new(30000); // 30 second epochs
        let now = Utc::now();
        let publish_time = now - chrono::Duration::seconds(75); // 75 seconds ago (2.5 epochs)

        tracker.record_publish(publish_time).await;

        let prediction = tracker.predict_next_epoch(now).await;
        assert!(prediction.is_some());

        let (seconds_until, _) = prediction.unwrap();

        // Should predict ~15 seconds until next (75 % 30 = 15, 30 - 15 = 15)
        assert!((seconds_until - 15.0).abs() < 0.2);
    }

    #[tokio::test]
    async fn test_tracker_updates_publish() {
        let tracker = EpochTracker::new(30000);
        let now = Utc::now();
        let t1 = now - chrono::Duration::seconds(60);
        let t2 = now - chrono::Duration::seconds(10);

        tracker.record_publish(t1).await;
        tracker.record_publish(t2).await;

        let (seconds_until, _) = tracker.predict_next_epoch(now).await.unwrap();

        // Should use the newer publish time (t2)
        assert!((seconds_until - 20.0).abs() < 0.2);
    }
}
