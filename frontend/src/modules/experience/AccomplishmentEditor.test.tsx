// @vitest-environment jsdom
import { act, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it } from 'vitest'
import AccomplishmentEditor, { type Accomplishment } from './AccomplishmentEditor'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
it('keeps action/outcome/metric together when reordering and removing accomplishments', async () => {
  function Test() { const [rows, setRows] = useState<Accomplishment[]>([{ action: 'Built API', outcome: 'Less latency', metric: '20%' }, { action: 'Wrote tests', outcome: 'Caught defects', metric: '12 cases' }]); return <><AccomplishmentEditor rows={rows} onChange={setRows} disabled={false} /><output>{JSON.stringify(rows)}</output></> }
  const container = document.createElement('div'); document.body.appendChild(container); const root = createRoot(container)
  const click = async (text: string) => act(async () => Array.from(container.querySelectorAll('button')).find(b => b.textContent === text)!.click())
  try { await act(async () => root.render(<Test />)); await click('Move up 2'); expect(JSON.parse(container.querySelector('output')!.textContent!)[0]).toEqual({ action: 'Wrote tests', outcome: 'Caught defects', metric: '12 cases' }); await click('Remove accomplishment 1'); expect(JSON.parse(container.querySelector('output')!.textContent!)).toEqual([{ action: 'Built API', outcome: 'Less latency', metric: '20%' }]); await click('Add accomplishment'); expect(container.querySelectorAll('input')).toHaveLength(6) } finally { await act(async () => root.unmount()); container.remove() }
})
