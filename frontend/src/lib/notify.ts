export function notify(message: string, type?: 'success' | 'error' | 'info') {
  const kind = type || (/fail|error|could not|invalid/i.test(message) ? 'error' : /saved|success|synced/i.test(message) ? 'success' : 'info')
  window.dispatchEvent(new CustomEvent('lifeos:notice', { detail: { message, type: kind } }))
}
