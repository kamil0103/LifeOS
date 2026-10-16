# Career workflow checkpoint

The Jobs screen supports keyword, location, status and source filters, sorting by recency/company/match score, and full job descriptions. A zero match score displays correctly. Job cards and application details link to the matching company research and generated documents.

Tracking an application opens a review dialog for the saved resume version and notes, then opens the resulting tracker entry. It records an application the user submitted; it does not submit applications to employers. Existing tracked jobs open their original entry. The backend reuses the existing application without overwriting its resume or original notes.

Repeated job saves are compared within the current account. HTTP(S) URLs are normalized for host, trailing slash and known advertising parameters; meaningful query parameters and fragments stay intact, so different requisitions remain separate. Entries without URLs use trimmed title/company/location. Existing duplicate records are retained. PostgreSQL advisory transaction locks and per-account process gates serialize simultaneous saves; no schema migration is needed.

Company research now has search, editing, company links and unsaved-change protection. Documents have keyword/type/job filters and links back to job details. Backend validation bounds research names, note lengths and ratings.

Linked resume versions and cover-letter documents cannot be deleted until the application reference is removed. A job with a tracked application requires explicit removal of the application first. Deleting an untracked job detaches document/calendar references and preserves those records. Editing a tracked job's application status directs the user to the tracker to keep the pipeline consistent.

Creation, editing and deletion rules live in `ICareerWorkflowService`/`CareerWorkflowService`; controllers delegate these mutations. The tracker continues using `/api/job-applications` to avoid the external gateway endpoint collision.

Validation: xUnit/Moq service and WebApplicationFactory API tests, Vitest component/filter tests, production builds, and `tests/career-live.cjs` with disposable PostgreSQL accounts. The live suite covers simultaneous job/application saves, distinct requisition IDs, original resume/notes preservation, cross-account protection, research editing and linked asset/job deletion safeguards.

Deployment images: `lifeos-api:career-phase5` and `lifeos-frontend:career-phase5`. Pre-deployment database/source/compose backups: `/mnt/user/appdata/lifeos/backups/20261014-career`. Rollback uses the prior compose image tags; no database rollback is necessary.

Remaining approved work includes AI Coach context/memory, learning and professional records, and personal modules/analytics. AI outputs remain suggestions that need user review.
