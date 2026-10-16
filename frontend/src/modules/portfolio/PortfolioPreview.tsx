import { useState } from 'react'
import type { PortfolioProject } from './PortfolioPage'

export function safeProjectUrl(value?: string) {
  try {
    const url = new URL(value || '')
    return ['https:', 'http:'].includes(url.protocol) && !url.username && !url.password ? url.href : undefined
  } catch { return undefined }
}

export function ProjectScreenshot({ project }: { project: PortfolioProject }) {
  const [failed, setFailed] = useState(false)
  const url = safeProjectUrl(project.screenshotUrl)
  if (!url || failed) return <p className="text-sm text-muted-foreground py-4">{project.screenshotUrl ? 'Screenshot unavailable.' : 'No screenshot added yet.'}</p>
  return <img src={url} alt={`${project.name} screenshot`} loading="lazy" referrerPolicy="no-referrer" onError={() => setFailed(true)} className="w-full max-h-96 object-contain rounded-md bg-secondary mb-3" />
}

export function PortfolioPreview({ project }: { project: PortfolioProject }) {
  return <div className="space-y-4 break-words">
    <ProjectScreenshot key={project.screenshotUrl} project={project} />
    <p className="whitespace-pre-wrap">{project.description || 'No description added yet.'}</p>
    {project.technologies && <p><strong>Technologies: </strong>{project.technologies}</p>}
    <p><strong>Dates: </strong>{project.startDate || 'Not specified'} – {project.isCurrent ? 'Present' : project.endDate || 'Not specified'}</p>
    {project.isFeatured && <p>Featured project</p>}
    {project.gitHubStars != null && <p>{project.gitHubStars} GitHub stars</p>}
    <div className="flex flex-wrap gap-4">
      {([['GitHub', project.gitHubRepoUrl], ['Live site', project.deployedUrl], ['Other link', project.link]] as const).map(([label, url]) => safeProjectUrl(url) && <a key={label} href={safeProjectUrl(url)} target="_blank" rel="noopener noreferrer" className="underline">{label}</a>)}
    </div>
    <section><h3 className="font-semibold mb-2">README</h3><p className="whitespace-pre-wrap text-sm">{project.readmePreview || 'No README preview stored for this project.'}</p></section>
  </div>
}
