// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { afterEach, expect, it, vi } from 'vitest'
import ResumeEditorPage from './ResumeEditorPage'
import api from '@/lib/api'
Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true})
vi.mock('@/hooks/useAuthStore',()=>({useAuthStore:(f:any)=>f({user:{id:'owner'}})}))
vi.mock('@/hooks/useUnsavedChanges',()=>({useUnsavedChanges:vi.fn()}))
vi.mock('@/lib/api',()=>({default:{get:vi.fn(),post:vi.fn(),put:vi.fn()}}))
const initial={title:'Original',template:'harvard',sectionOrder:['summary'],profile:{fullName:'Candidate',email:'test@example.invalid',phone:'',location:'',linkedIn:'',portfolio:'',github:'',summary:'Original summary'},experience:[],education:[],skills:[],projects:[],certifications:[],pendingRecommendations:[]}
afterEach(()=>{vi.resetAllMocks();sessionStorage.clear()})
async function mount(){vi.mocked(api.get).mockImplementation(async p=>({data:p==='/resume-draft'?{revision:'revision',data:initial}:p==='/jobs'?[{id:'job',title:'Engineer',company:'Devices'}]:p==='/resume-versions'?[{id:'version',title:'Saved version',template:'harvard',createdAt:'2026-10-16'}]:{data:{...initial,title:'Older saved title'}}}));const el=document.createElement('div');document.body.append(el);const root=createRoot(el);await act(async()=>root.render(<ResumeEditorPage/>));return{el,click:async(text:string)=>act(async()=>Array.from(el.querySelectorAll('button')).find(b=>b.textContent?.trim()===text)!.click()),close:async()=>{await act(async()=>root.unmount());el.remove()}}}
it('restores the server draft and compares a version without changing the draft',async()=>{
 const v=await mount();try{expect(v.el.textContent).toContain('Server draft restored');expect(api.get).not.toHaveBeenCalledWith('/documents/resume-data');await v.click('Versions');await v.click('Compare with draft');expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Older saved title');expect(api.put).not.toHaveBeenCalled();expect(api.post).not.toHaveBeenCalled()}finally{await v.close()}
})
it('reviews AI changes before applying and never automatically saves a version',async()=>{
 vi.mocked(api.post).mockResolvedValue({data:{tailoredResume:{...initial,profile:{...initial.profile,summary:'AI proposed summary'}},changeSummary:['Changed summary'],recommendations:[]}})
 const v=await mount();try{const select=Array.from(v.el.querySelectorAll('select')).find(s=>Array.from(s.options).some(o=>o.value==='job'))!;await act(async()=>{select.value='job';select.dispatchEvent(new Event('change',{bubbles:true}))});await v.click('Tailor Resume');await v.click('Review changes to current draft');const dialog=document.querySelector('[role="dialog"]')!;expect(dialog.textContent).toContain('AI proposed summary');expect(api.post).toHaveBeenCalledTimes(1);expect(api.post).not.toHaveBeenCalledWith('/resume-versions',expect.anything());expect(api.put).not.toHaveBeenCalled()
 await act(async()=>Array.from(dialog.querySelectorAll('button')).find(b=>b.textContent==='Apply to draft')!.click());expect(document.querySelector('[role="dialog"]')).toBeNull();expect(api.post).toHaveBeenCalledTimes(1)
 }finally{await v.close()}
})
it('opens a source-version link as a comparison without replacing the server draft',async()=>{
 window.history.replaceState(null,'','/resume?version=source')
 const v=await mount();try{await act(async()=>{await Promise.resolve()});expect(api.get).toHaveBeenCalledWith('/resume-versions/source');expect(document.querySelector('[role="dialog"]')?.textContent).toContain('Older saved title');expect(v.el.querySelector<HTMLInputElement>('input')?.value).toBe('Original');expect(api.put).not.toHaveBeenCalled()}finally{await v.close();window.history.replaceState(null,'','/')}
})
