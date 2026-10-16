import { expect, it } from 'vitest'
import { filterJobs } from './job-filters'
const jobs = [
  { title: 'Firmware engineer', company: 'Router Co', description: 'Embedded C++', location: 'Irvine', status: 'saved', source: 'manual', matchScore: 0, createdAt: '2026-10-14' },
  { title: 'Web engineer', company: 'Web Co', description: 'React', location: 'Remote', status: 'interview', source: 'rss', matchScore: 90, createdAt: '2026-10-13' },
  { title: 'Developer', company: 'Other', description: 'Python', status: 'saved', source: 'manual', createdAt: '2026-10-12' },
]
const base = { search: '', status: '', source: '', location: '', sort: 'newest' }
it('combines search, location, source and status filters', () => { expect(filterJobs(jobs, { ...base, search: 'C++', location: 'irvine', status: 'saved', source: 'manual' })).toEqual([jobs[0]]) })
it('keeps a real zero match score above an unscored job', () => { expect(filterJobs(jobs, { ...base, sort: 'match' })).toEqual([jobs[1], jobs[0], jobs[2]]) })
