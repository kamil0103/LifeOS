# Habit schedules and progress

Habits supports Today, All active and Archived views. Today, Dashboard and daily planning include only active habits scheduled for the user's current local weekday. Days use the profile timezone (Sunday bit 0 through Saturday bit 6). Existing habits retain their prior every-day behavior until edited; legacy frequency labels were not previously operational schedules.

Users can edit names/categories, optional numeric targets/units and selected weekdays. Edits check the saved revision. Archive hides a habit without deleting history, streak records or XP; restore reuses that same record. Archive/restore and progress require ownership. Archived habits cannot receive new progress.

Numeric progress is the **total for the current local day**, not an increment. A daily progress row stays partial until the target is reached. Only the first target crossing awards XP and advances the streak. Retrying a total does not create another row or XP award. Completed progress cannot be reduced, and changing a target does not revoke a previously earned achievement. Boolean habits retain the Complete action; repeat completion returns HTTP 400. Numeric daily-plan actions link to Habits rather than completing the target without a value.

Scheduled off-days do not break a streak. Missing a scheduled day resets the current streak when the next achievement is recorded, and the displayed current streak becomes zero once a prior scheduled day has been missed. Editing the schedule applies the current weekday pattern when evaluating future streak continuity; the app does not retain a full history of past schedule definitions. The longest recorded streak and XP are preserved.

Partial progress is excluded from Dashboard completion totals/weekly bars, Analytics completion heatmaps and Coach completion counts. Coach also receives the due-today flag. Historical completion counts remain activity counts; schedule-based adherence denominators and outcome drill-down remain separate open Analytics work.

The additive HabitSchedulesAndProgress migration adds `Habits.ScheduleDaysMask` with default 127 and `HabitCompletions.IsCompleted` with default true, preserving existing records. New partial rows explicitly set IsCompleted=false. No history or XP is deleted or backfilled with inferred values.

Validation covers xUnit/Moq schedule/target/ownership/revision behavior, WebApplicationFactory routes, timezone/DST cases, Vitest form/progress/archive recovery, and `tests/habits-live.cjs` for PostgreSQL concurrency, exactly-once XP, due-day filtering, stale edits and archive/restore history. Live checks use disposable accounts.
