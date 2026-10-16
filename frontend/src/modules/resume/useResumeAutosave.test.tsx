// @vitest-environment jsdom
import { act, useEffect, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { afterEach, expect, it, vi } from 'vitest'
import { useResumeAutosave } from './useResumeAutosave'
import api from '@/lib/api'
Object.assign(globalThis,{IS_REACT_ACT_ENVIRONMENT:true})
vi.mock('@/lib/api',()=>({default:{put:vi.fn()}}))
afterEach(()=>{vi.useRealTimers();vi.resetAllMocks()})
async function mount(){let edit:(value:string)=>void=()=>{};function Harness(){const[data,setData]=useState({title:'Initial'});const save=useResumeAutosave(data);edit=value=>setData({title:value});useEffect(()=>save.initialize({title:'Initial'},'r0'),[]);return <p>{save.status} {save.error}</p>};const el=document.createElement('div');const root=createRoot(el);await act(async()=>root.render(<Harness/>));return{el,edit:async(v:string)=>act(async()=>edit(v)),close:async()=>act(async()=>root.unmount())}}
it('serializes in-flight saves and uses the returned revision for later edits',async()=>{
 vi.useFakeTimers();let resolve:(v:any)=>void=()=>{};vi.mocked(api.put).mockImplementationOnce(()=>new Promise(r=>resolve=r)).mockResolvedValueOnce({data:{revision:'r2'}})
 const v=await mount();try{await v.edit('First');await act(async()=>vi.advanceTimersByTimeAsync(1100));expect(api.put).toHaveBeenCalledTimes(1);await v.edit('Second');await act(async()=>vi.advanceTimersByTimeAsync(1100));expect(api.put).toHaveBeenCalledTimes(1)
 await act(async()=>resolve({data:{revision:'r1'}}));await act(async()=>vi.advanceTimersByTimeAsync(1100));expect(api.put).toHaveBeenLastCalledWith('/resume-draft',{data:{title:'Second'},expectedRevision:'r1'});expect(v.el.textContent).toContain('Draft saved to server')
 }finally{await v.close()}
})
it('pauses after a conflict and keeps new local edits from overwriting the server',async()=>{
 vi.useFakeTimers();vi.mocked(api.put).mockRejectedValue({response:{status:409,data:{detail:'Newer draft exists'}}});const v=await mount();try{await v.edit('Local');await act(async()=>vi.advanceTimersByTimeAsync(1100));expect(v.el.textContent).toContain('Newer draft exists');await v.edit('More local text');await act(async()=>vi.advanceTimersByTimeAsync(5000));expect(api.put).toHaveBeenCalledTimes(1)}finally{await v.close()}
})
