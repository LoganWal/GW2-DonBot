import { useConfirm } from 'primevue/useconfirm'

type ConfirmActionOptions = {
  message: string
  header?: string
  acceptLabel?: string
  rejectLabel?: string
  acceptIcon?: string
  rejectIcon?: string
  severity?: 'danger' | 'warn' | 'info' | 'success' | 'secondary' | 'primary'
}

/**
 * Generic confirm modal wrapper. Returns a promise that resolves true when
 * the user accepts and false when they reject or dismiss the dialog.
 */
export const useConfirmAction = () => {
  const confirm = useConfirm()

  const ask = (opts: ConfirmActionOptions): Promise<boolean> => new Promise((resolve) => {
    confirm.require({
      message: opts.message,
      header: opts.header ?? 'Are you sure?',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: opts.acceptLabel ?? 'Confirm',
      rejectLabel: opts.rejectLabel ?? 'Cancel',
      acceptProps: {
        severity: opts.severity ?? 'primary',
        icon: opts.acceptIcon,
      },
      rejectProps: {
        severity: 'secondary',
        outlined: true,
        icon: opts.rejectIcon,
      },
      accept: () => resolve(true),
      reject: () => resolve(false),
      onHide: () => resolve(false),
    })
  })

  const confirmDelete = (opts: Omit<ConfirmActionOptions, 'severity' | 'acceptIcon'> & { acceptLabel?: string }) =>
    ask({
      header: 'Delete',
      acceptLabel: 'Delete',
      acceptIcon: 'pi pi-trash',
      severity: 'danger',
      ...opts,
    })

  return { ask, confirmDelete }
}
