import { describe, expect, it } from 'vitest'
import { addDays, dateKey, dateTimeInput, zonedInputToIso } from './calendar-time'
describe('calendar timezone conversion', () => {
  it('uses the profile timezone rather than the browser timezone', () => {
    expect(zonedInputToIso('2026-10-14T09:00', 'America/Los_Angeles')).toBe('2026-10-14T16:00:00.000Z')
    expect(dateTimeInput('2026-10-14T16:00:00Z', 'America/Los_Angeles')).toBe('2026-10-14T09:00')
    expect(dateKey('2026-10-15T01:00:00Z', 'America/Los_Angeles')).toBe('2026-10-14')
  })
  it('handles month rollover and daylight-saving days', () => {
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01')
    const start = Date.parse(zonedInputToIso('2026-03-08T00:00', 'America/Los_Angeles'))
    const end = Date.parse(zonedInputToIso('2026-03-09T00:00', 'America/Los_Angeles'))
    expect((end - start) / 3600000).toBe(23)
  })
  it('rejects nonexistent times during the spring clock change', () => {
    expect(() => zonedInputToIso('2026-03-08T02:30', 'America/Los_Angeles')).toThrow('does not exist')
  })
})
