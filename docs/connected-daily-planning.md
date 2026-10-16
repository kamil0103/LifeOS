# Connected daily planning

Phase 4 adds a single dashboard action list for habits, due application follow-ups, individual mission items and spaced coding reviews, alongside today's local and connected Google commitments. Dates use the user's profile timezone. Google failures display a warning while local planning stays available.

Users can complete actions or reserve time in their local calendar or their own dedicated LifeOS Google calendar. Native scheduling uses a database unique key per action occurrence; Google scheduling uses a deterministic provider event ID. A habit can be scheduled again on a later day. Existing Google calendars remain read-only.

Application details now expose recruiter contacts, owned resume and cover-letter references, notes, a timeline and accessible status selectors. Setting a follow-up creates one linked local event. Changing its time updates the application; deleting it clears the follow-up. Concurrent setters serialize on the application row. Local scheduling of a follow-up updates that linked event, including its selected duration.

In-app reminders refresh every five minutes and when the daily plan is loaded. Users can enable categories, set calendar lead time, mark read, snooze for an hour, or dismiss. A dismissed occurrence is retained as an archived record so the worker does not recreate it. A changed due time produces a new occurrence. The sidebar shows an unread count. Notification and commitment links open the source application or calendar event.

Coding reviews use 1, 3, 7, 14 and 30-day intervals based on attempt count. Review completion records one zero-XP attempt and advances the next review without inflating XP. Individual mission completion preserves the other priorities and marks the mission complete after all items are done.

## Validation

- xUnit/Moq service tests and WebApplicationFactory API tests cover ownership, review spacing, daily scheduling keys, follow-up/calendar synchronization, invalid references/statuses and reminders.
- Vitest component/date tests cover calendar details, daily plan completion/calendar choice, notification links/snooze/filter and unsaved-change handling.
- `tests/planning-live.cjs` checks the deployed API with disposable users against PostgreSQL: concurrent follow-up setters, idempotent scheduling, concurrent coding reviews and mission items, ownership, reminder deduplication/snooze/dismiss/preferences, and calendar synchronization. Its `finally` block deletes those test users.
- Production frontend/API images build successfully. Migration `20261014213208_ConnectedDailyPlanning` is additive.

## Deployment checkpoint

Deployed images: `lifeos-api:planning-phase4` and `lifeos-frontend:planning-phase4`.

Pre-deployment database, source, compose configuration and protected integration-data backups are in `/mnt/user/appdata/lifeos/backups/20261014-planning`. The Google connection remains per user. Credentials are stored outside the repository.

For application rollback, restore the compose image tags from the backup and recreate API/frontend/nginx. The previous images tolerate the additive schema; do not downgrade or restore the database merely to roll back images, because that would discard newer data.

This checkpoint completes connected planning and the application detail foundation. The remaining approved career, AI Coach, learning/professional-record and personal/analytics improvements are not represented as complete here.
