// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it, vi } from 'vitest'
import ResumePdfPreview from './ResumePdfPreview'
import api from '@/lib/api'
Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true})
vi.mock('@/lib/api',()=>({default:{post:vi.fn(async()=>({data:'%PDF-synthetic'}))}}))
it('renders an authenticated PDF blob with download fallback and releases it on close',async()=>{
 const create=vi.fn(()=> 'blob:preview');const revoke=vi.fn();Object.assign(URL,{createObjectURL:create,revokeObjectURL:revoke});const el=document.createElement('div');document.body.append(el);const root=createRoot(el)
 try{await act(async()=>root.render(<ResumePdfPreview data={{title:'Current draft'}}/>));await act(async()=>el.querySelector('button')!.click());expect(api.post).toHaveBeenCalledWith('/resume-draft/preview',{data:{title:'Current draft'}},{responseType:'blob'});const dialog=document.querySelector('[role="dialog"]')!;expect(dialog.querySelector('iframe')?.getAttribute('src')).toBe('blob:preview');expect(dialog.querySelector('a')?.hasAttribute('download')).toBe(true);await act(async()=>dialog.querySelector<HTMLButtonElement>('[aria-label="Close dialog"]')!.click());expect(revoke).toHaveBeenCalledWith('blob:preview')
 }finally{await act(async()=>root.unmount());el.remove()}
})
