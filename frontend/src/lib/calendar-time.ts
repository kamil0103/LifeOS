export function dateKey(value: Date | string, zone: string) {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date(value))
  const part = (name: string) => parts.find(p => p.type === name)?.value
  return `${part('year')}-${part('month')}-${part('day')}`
}
export function addDays(key: string, days: number) {
  const value = new Date(key + 'T12:00:00Z'); value.setUTCDate(value.getUTCDate() + days)
  return value.toISOString().slice(0, 10)
}
export function dateTimeInput(value: string, zone: string) {
  const parts = new Intl.DateTimeFormat('en-US', { timeZone: zone, year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).formatToParts(new Date(value))
  const part = (name: string) => parts.find(p => p.type === name)?.value
  return `${part('year')}-${part('month')}-${part('day')}T${part('hour')}:${part('minute')}`
}
export function zonedInputToIso(value: string, zone: string) {
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value)) throw new Error('Enter a valid date and time.')
  const target = Date.parse(value + ':00Z'); if (!Number.isFinite(target)) throw new Error('Enter a valid date and time.')
  let instant = target
  for (let i = 0; i < 4; i++) {
    const shown = Date.parse(dateTimeInput(new Date(instant).toISOString(), zone) + ':00Z')
    instant += target - shown
  }
  const result = new Date(instant).toISOString()
  if (dateTimeInput(result, zone) !== value) throw new Error('That time does not exist because of a daylight-saving change. Choose another time.')
  return result
}
