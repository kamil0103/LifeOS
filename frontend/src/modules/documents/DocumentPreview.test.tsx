// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it, vi } from 'vitest'
import DocumentPreview from './DocumentPreview'
import api from '@/lib/api'
Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true})
vi.mock('@/lib/api',()=>({default:{get:vi.fn()}}))
it('loads a stored PDF through the authenticated endpoint and revokes its preview URL',async()=>{
 vi.mocked(api.get).mockResolvedValue({data:'%PDF-test'});const revoke=vi.fn();Object.assign(URL,{createObjectURL:vi.fn(()=> 'blob:stored-pdf'),revokeObjectURL:revoke});const el=document.createElement('div');document.body.append(el);const root=createRoot(el)
 try{await act(async()=>root.render(<DocumentPreview id="doc" filename="resume.pdf"/>));await act(async()=>el.querySelector('button')!.click());expect(api.get).toHaveBeenCalledWith('/documents/doc/preview',{responseType:'blob'});const dialog=document.querySelector('[role="dialog"]')!;expect(dialog.querySelector('iframe')?.getAttribute('src')).toBe('blob:stored-pdf');expect(dialog.querySelector('a')?.getAttribute('download')).toBe('resume.pdf');await act(async()=>dialog.querySelector<HTMLButtonElement>('[aria-label="Close dialog"]')!.click());expect(revoke).toHaveBeenCalledWith('blob:stored-pdf')
 }finally{await act(async()=>root.unmount());el.remove()}
})
