// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it } from 'vitest'
import { PortfolioPreview, safeProjectUrl } from './PortfolioPreview'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
it('renders README as text, rejects unsafe links and handles broken screenshots', async () => {
  const el = document.createElement('div'); const root = createRoot(el)
  try {
    await act(async () => root.render(<PortfolioPreview project={{ id: 'p', name: 'Demo', isCurrent: true, isFeatured: true, screenshotUrl: 'https://example.com/image.png', deployedUrl: 'javascript:alert(1)', readmePreview: '<script>alert(1)</script>' }} />))
    expect(el.querySelector('script')).toBeNull(); expect(el.textContent).toContain('<script>alert(1)</script>'); expect(el.querySelector('a')).toBeNull()
    expect(el.querySelector('img')?.alt).toBe('Demo screenshot')
    await act(async () => el.querySelector('img')!.dispatchEvent(new Event('error')))
    expect(el.textContent).toContain('Screenshot unavailable.'); expect(el.querySelector('img')).toBeNull()
    expect(safeProjectUrl('https://user:pass@example.com')).toBeUndefined()
    expect(safeProjectUrl('https://example.com')).toBe('https://example.com/')
  } finally { await act(async () => root.unmount()) }
})
