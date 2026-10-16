// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { expect, it, vi } from 'vitest'
import DocumentLinksEditor from './DocumentLinksEditor'
import api from '@/lib/api'
Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true})
vi.mock('@/lib/api',()=>({default:{get:vi.fn(async(p:string)=>({data:p==='/jobs'?[{id:'job',title:'Engineer',company:'Devices'}]:p==='/job-applications'?[{id:'app',jobId:'job',jobTitle:'Engineer',company:'Devices'}]:[{id:'version',title:'Original resume'}]})),put:vi.fn(async()=>{throw{response:{data:{detail:'Document links changed'}}}})}}))
it('infers the application job, sends the source/revision and retains choices after conflicts',async()=>{
 const el=document.createElement('div');document.body.append(el);const root=createRoot(el);const saved=vi.fn()
 try{await act(async()=>root.render(<DocumentLinksEditor document={{id:'doc',filename:'resume.pdf',updatedAt:'2026-10-16T00:00:00Z'}} onClose={vi.fn()} onSaved={saved}/>));const dialog=document.querySelector('[role="dialog"]')!;const selects=dialog.querySelectorAll('select')
 await act(async()=>{selects[0].value='app';selects[0].dispatchEvent(new Event('change',{bubbles:true}));selects[2].value='version';selects[2].dispatchEvent(new Event('change',{bubbles:true}))})
 // Select source after React has applied the application update.
 await act(async()=>{selects[2].value='version';selects[2].dispatchEvent(new Event('change',{bubbles:true}))})
 expect(selects[1].value).toBe('job');expect(selects[1].disabled).toBe(true)
 await act(async()=>dialog.querySelector('form')!.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})))
 expect(api.put).toHaveBeenCalledWith('/documents/doc/links',{jobId:'job',applicationId:'app',sourceResumeVersionId:'version',expectedUpdatedAt:'2026-10-16T00:00:00Z'});expect(dialog.textContent).toContain('Document links changed');expect(selects[0].value).toBe('app');expect(saved).not.toHaveBeenCalled()
 }finally{await act(async()=>root.unmount());el.remove()}
})
