// @vitest-environment jsdom
import { act } from 'react'
import { createRoot } from 'react-dom/client'
import { beforeEach, expect, it, vi } from 'vitest'
import MissionWorkflow from './MissionWorkflow'
import api from '@/lib/api'
Object.assign(globalThis, { IS_REACT_ACT_ENVIRONMENT: true })
vi.mock('@/lib/api', () => ({ default: { get: vi.fn(), post: vi.fn(), put: vi.fn(), delete: vi.fn() } }))
beforeEach(() => vi.resetAllMocks())
const tasks = [{ id: 'a', title: 'Practice C++', category: 'coding', priority: 'high', reason: 'Review' }, { id: 'b', title: 'Reflect', category: 'personal', priority: 'low', reason: 'Learn' }]
const proposal = { id: 'draft', date: '2026-10-16T00:00:00Z', timeZone: 'America/Los_Angeles', priorities: tasks, aiSummary: 'Focus' }
async function mount() { const el=document.createElement('div');document.body.append(el);const root=createRoot(el);await act(async()=>root.render(<MissionWorkflow/>));return {el,click:async(text:string)=>act(async()=>Array.from(el.querySelectorAll('button')).find(b=>b.textContent===text)!.click()),close:async()=>{await act(async()=>root.unmount());el.remove()}} }
async function input(node: HTMLInputElement, value: string) {await act(async()=>{Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value')!.set!.call(node,value);node.dispatchEvent(new Event('input',{bubbles:true}))})}
async function submit(el: HTMLElement){await act(async()=>el.querySelector('form')!.dispatchEvent(new Event('submit',{bubbles:true,cancelable:true})))}
it('reviews and edits generated suggestions without applying them, then retains failed reviews',async()=>{
 vi.mocked(api.get).mockRejectedValue({response:{status:404}});vi.mocked(api.post).mockResolvedValueOnce({data:proposal}).mockRejectedValueOnce({response:{data:{detail:'Mission changed'}}})
 const v=await mount();try{await v.click('Suggest mission');expect(api.post).toHaveBeenCalledTimes(1);expect(v.el.textContent).toContain('Review suggested mission');expect(v.el.textContent).toContain('Nothing enters your daily plan')
 await input(v.el.querySelector('input')!,'Reviewed C++ task');await act(async()=>v.el.querySelector<HTMLButtonElement>('[aria-label="Move task 2 up"]')!.click());await submit(v.el)
 expect(api.post).toHaveBeenLastCalledWith('/aicoach/mission/proposal/draft/apply',expect.objectContaining({priorities:[expect.objectContaining({id:'b'}),expect.objectContaining({id:'a',title:'Reviewed C++ task'})]}));expect(v.el.textContent).toContain('Mission changed');expect(Array.from(v.el.querySelectorAll('input')).some(i=>i.value==='Reviewed C++ task')).toBe(true)
 }finally{await v.close()}
})
it('preserves completed tasks and sends the loaded mission revision',async()=>{
 const mission={...proposal,id:'mission',updatedAt:'2026-10-16T01:00:00Z',isCompleted:false,priorities:[{...tasks[0],completed:true},tasks[1]]}
 vi.mocked(api.get).mockImplementation(async path=>{if(path==='/aicoach/mission')return{data:mission};throw{response:{status:404}}});vi.mocked(api.put).mockResolvedValue({data:mission})
 const v=await mount();try{await v.click('Edit mission');expect(v.el.querySelector<HTMLInputElement>('fieldset input')!.disabled).toBe(true);expect(v.el.querySelector<HTMLButtonElement>('[aria-label="Remove task 1"]')!.disabled).toBe(true)
 await submit(v.el);expect(api.put).toHaveBeenCalledWith('/aicoach/mission/mission',expect.objectContaining({expectedUpdatedAt:mission.updatedAt}));expect(v.el.querySelector('form')).toBeNull()
 }finally{await v.close()}
})
it('recovers a pending suggestion and discards it without creating a mission',async()=>{
 vi.mocked(api.get).mockImplementation(async path=>{if(path==='/aicoach/mission/proposal')return{data:proposal};throw{response:{status:404}}});vi.mocked(api.delete).mockResolvedValue({})
 const v=await mount();try{expect(v.el.textContent).toContain('waiting for review');await v.click('Discard suggestion');expect(api.delete).toHaveBeenCalledWith('/aicoach/mission/proposal/draft');expect(api.post).not.toHaveBeenCalled();expect(v.el.textContent).toContain('Suggest mission')}finally{await v.close()}
})
