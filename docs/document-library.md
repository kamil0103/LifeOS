# Document previews and reference links — pending server validation

Status: implementation prepared; local production frontend build and 61 frontend tests pass. Tower stopped responding to Desktop Commander requests. Backend tests, EF migration generation, PostgreSQL checks, backup and deployment have not been completed for this batch. The currently deployed release remains resume-review-phase18. Do not deploy this checkpoint until the migration and validation gates pass.

Documents now offer an authenticated inline PDF preview with a download fallback and blob URL cleanup. The preview service checks document ownership before reading storage, accepts only PDF bytes, and uses no-store/nosniff headers. Download remains available when the browser cannot display the preview.

Edit links associates a document with an owned job, application and source resume version. Selecting an application infers its job; inconsistent/foreign targets are rejected. Saving requires the loaded revision and retains the form on conflicts. These are reference/provenance links: they do not select a resume or cover letter in Application Tracker, verify that a PDF matches a saved version, or change the stored PDF bytes.

The list shows application/source links, searches linked labels and filters by application or job. Application Tracker retains its job-documents link and adds an application-documents link. A source resume opens a comparison against the current draft, without replacing or autosaving that draft. Generating a resume after explicitly loading a saved version can record that version as its source. The source is known for the current editor session; arbitrary restored drafts do not invent provenance. Other/legacy documents can be linked manually.

Two nullable Document columns and relationships are required: ApplicationId and SourceResumeVersionId. Both use SetNull on referenced deletion. Normal resume-version deletion is blocked while a document references it; clear the source link first. Metadata edits do not create documents, change PDF contents or alter application selections. Existing documents receive no inferred links. Existing generated job cover letters retain their job association.

Architecture: Application DTOs/IDocumentLibraryService, Infrastructure ownership/validation/preview/metadata operations and a delegating controller. Preview never exposes storage paths. Existing document download/delete behavior remains, including protection for cover letters selected by applications. Source validation runs before resume PDF generation/storage.

Resume after reconnecting Tower:
1. Inspect whether the timed-out transfer/process staged any files or generated a DocumentReferenceLinks migration; do not assume completion or blindly repeat side effects.
2. Sync the checkpoint source, generate the code-first migration if absent, retrieve its designer/snapshot and verify it adds only the intended nullable columns/indexes/foreign keys.
3. Run all backend tests and snapshot checks. The five new DocumentLibraryTests cover metadata, ownership, stale revisions, PDF guards, generation validation and source deletion protection.
4. Take database/source/compose backups; apply the additive migration; build API/frontend with CPU 0.
5. Run documents-library-live.cjs in a temporary Testing API before deployment and against production afterwards. It exercises real stored PDF preview, source/application metadata, concurrency/ownership and cleanup without AI calls.
6. Verify session restoration and public routing, preserve CPU 0, update the approved-plan audit/draft PR, and merge the checkpoint into the feature branch only after validation. General browser/mobile/accessibility and native PDF-plugin QA remain open.
