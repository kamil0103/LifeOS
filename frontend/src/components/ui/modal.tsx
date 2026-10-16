import * as Dialog from '@radix-ui/react-dialog'
import { X } from 'lucide-react'
import type { ReactNode } from 'react'
export function Modal({ open, onClose, title, children }: { open: boolean; onClose: () => void; title: string; children: ReactNode }) {
  return <Dialog.Root open={open} onOpenChange={value => { if (!value) onClose() }}><Dialog.Portal>
    <Dialog.Overlay className="fixed inset-0 bg-black/60 z-50" />
    <Dialog.Content className="fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-50 w-[calc(100%-2rem)] max-w-xl max-h-[90vh] overflow-y-auto rounded-xl bg-card border p-5 shadow-xl">
      <div className="flex justify-between gap-4 mb-4"><Dialog.Title className="font-semibold text-lg">{title}</Dialog.Title><button onClick={onClose} aria-label="Close dialog"><X size={20} /></button></div>
      <Dialog.Description className="sr-only">View or edit details.</Dialog.Description>{children}
    </Dialog.Content>
  </Dialog.Portal></Dialog.Root>
}
