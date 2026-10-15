# Learning record improvements

Coding attempts now expose language, duration, XP and approach/review notes in the Coding Tracker. A solved problem can be reviewed directly. The existing daily plan review action remains available.

A problem receives its initial difficulty XP once. Further days receive review XP (5). One recorded entry per problem per user-local day prevents duplicate clicks, network retries and unsolve/solve loops from multiplying XP or advancing the review interval. Different notes on an already recorded day are rejected with an explanatory error; the UI keeps the draft. Daily plan reviews retain their existing zero-XP behavior. Marking unsolved keeps history and historical coding XP. Deleting a problem deletes its attempts; previously earned account XP remains in the ledger.

Coding writes use a per-user gate, PostgreSQL advisory transaction lock and problem row lock. The row lock coordinates with daily-plan review completion. Request validation rejects invalid durations, difficulty, names and unsafe problem URLs.

Education parent links must belong to the signed-in user. Courses inherit their degree's institution; conflicting parents are rejected. Moving a degree moves its courses' institution. Deleting a degree retains its courses under the institution as unassigned. Institution views show degree courses once and exclude foreign-owned related records.

No schema migration is required. Existing records and OAuth integration files are preserved.
