# Google Calendar setup

LifeOS reads selected Google calendars and edits only its dedicated LifeOS calendar. Google remains the source of truth for Google events; local events remain local. No external calendar data is copied into the native event table. A periodic five-minute refresh keeps a bounded cache from 30 days ago through 180 days ahead. Other periods load on demand. The initial integration uses window refreshes, not incremental sync tokens or webhooks.

## One-time configuration

1. Open Google Cloud Console and create or select a project.
2. Enable Google Calendar API.
3. Configure Google Auth Platform branding, audience, consent scopes and your test account if using Testing status.
4. Create a **Web application** OAuth client.
5. Add this authorized redirect URI, replacing the host with your HTTPS LifeOS address:
   `https://YOUR-LIFEOS-HOST/api/googlecalendar/callback`
6. Add the values to the Unraid Compose Manager LifeOS project's `.env` file:

   ```dotenv
   GOOGLE_CALENDAR_CLIENT_ID=your-client-id
   GOOGLE_CALENDAR_CLIENT_SECRET=your-client-secret
   GOOGLE_CALENDAR_REDIRECT_URI=https://YOUR-LIFEOS-HOST/api/googlecalendar/callback
   ```

7. Recreate the API through Compose Manager. Do not commit the secret to GitHub.
8. Open Calendar → Google Calendar → Connect Google Calendar. Complete Google's account selection and consent screen.
9. Choose calendars and Save Selection. Existing calendars are read-only in LifeOS; the dedicated LifeOS calendar is editable.

Requested scopes: `openid`, `email`, `calendar.readonly`, `calendar.app.created`.

Google's OAuth callback must reach the API through your reverse proxy. Use the same LifeOS host for starting and returning from Google sign-in so the short-lived browser binding cookie is present. No publicly reachable webhook is required for periodic refreshes.

## Persistence and recovery

The API mounts `/app/data/integrations` from LifeOS appdata. OAuth credentials and cached calendar data are protected with ASP.NET Data Protection. Both the protected account files and the keys directory must be backed up and restored together. Connection files use owner-only permissions on Linux. Keep the integration directory private to the server administrators.

Disconnect revokes Google authorization and removes the local connection/cache. It does not delete calendars or events from Google. Reconnecting the same account reuses the stored dedicated calendar when the connection is still present. After disconnecting and connecting again, a new dedicated calendar may be created; the old one is retained.

Google event updates/deletes use ETags. If another client changed the event, LifeOS returns a conflict instead of overwriting it. Edits to expanded recurring events apply to that occurrence.

By default AI Coach receives only Google busy times. Sharing event details is opt-in. Local LifeOS event titles are included in daily mission context.

## Verification

Automated tests cover local event validation/ownership, OAuth one-time state and browser binding, scope selection, encryption/persistence, Google event permissions and conflict responses, pagination and recurrence/all-day mapping, and timezone conversion. A real sign-in, calendar selection and cross-app edit test still require the user's OAuth client and Google consent.

Official references:
- https://developers.google.com/identity/protocols/oauth2/web-server
- https://developers.google.com/workspace/calendar/api/auth
- https://developers.google.com/workspace/calendar/api/v3/reference/events/list
- https://developers.google.com/workspace/calendar/api/guides/version-resources
