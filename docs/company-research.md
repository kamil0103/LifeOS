# Company research

Company Notes now separates website, industry, products/services, technology stack, culture/working style and interview preparation from general notes. Ratings may be unset. Search includes every research field. Website links accept HTTP/HTTPS without embedded credentials and are never fetched by the server.

Expand **View related jobs** on a saved research note to see owned saved jobs with the same company name, ignoring case and outer whitespace. Each result links to job details, shows location/salary where saved, and links to a tracked application with its current status. No substring/fuzzy matching or automatic merge is performed: “Example” and “Example Labs” remain separate. Renaming the note changes its related-job results; it does not rename jobs. Removing research does not delete jobs or applications.

The editor sends its loaded revision. A stale save returns 409 and keeps the form text; copy the desired edits before closing and reopening the latest record. New fields omitted by older clients are preserved; an empty string clears an individual field. Legacy general notes, ratings, timestamps and duplicate company notes are retained. This batch does not deduplicate creates after ambiguous network failures or introduce source citations/automatic company research.

Implementation: Application DTOs and ICompanyResearchService; Infrastructure validates and stores records and resolves related jobs; the controller delegates. Revision writes use per-user PostgreSQL transaction locks, and revision timestamps use millisecond precision. Migration `20261016060100_StructuredCompanyResearch` adds six nullable text columns without rewriting existing data.

Validation includes service and WebApplicationFactory tests for ownership, structured-field preservation, clearing, validation, conflicts and exact company matching; frontend tests for related navigation and retained form data after a conflict; a disposable PostgreSQL live test for simultaneous versioned edits and older-client compatibility. General browser/mobile/accessibility review remains open. Interview scheduling/history is a separate next batch.
