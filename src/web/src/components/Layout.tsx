import type { ReactNode } from 'react'
import { useMe } from '../api/hooks'
import { useUi } from '../store/ui'

export function Layout({ children }: { children: ReactNode }) {
  const { view, setView } = useUi()
  return (
    <div className="min-h-full px-6 py-4">
      <header className="flex items-center justify-between mb-6 pb-3 border-b border-rule">
        <div className="flex items-baseline gap-6">
          <h1 className="text-2xl">Longevity</h1>
          <nav className="flex gap-0.5">
            <NavTab active={view === 'gallery'} onClick={() => setView('gallery')}>
              Gallery
            </NavTab>
            <NavTab active={view === 'vocabulary'} onClick={() => setView('vocabulary')}>
              Vocabulary
            </NavTab>
          </nav>
        </div>
        <LoginDisplay />
      </header>
      <main>{children}</main>
    </div>
  )
}

function NavTab({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: ReactNode
}) {
  return (
    <button
      onClick={onClick}
      className={[
        'px-3 pb-1 text-sm border-b-2 transition-colors',
        active
          ? 'border-accent text-ink font-medium'
          : 'border-transparent text-muted hover:text-ink',
      ].join(' ')}
    >
      {children}
    </button>
  )
}

function LoginDisplay() {
  const { data, isPending } = useMe()
  if (isPending) return <span className="text-sm text-muted">…</span>
  if (data?.email) {
    return (
      <div className="flex items-center gap-3 text-sm">
        <span className="text-muted">{data.email}</span>
        <form method="post" action="/auth/logout">
          <button
            type="submit"
            className="text-accent underline-offset-2 hover:underline"
          >
            Sign out
          </button>
        </form>
      </div>
    )
  }
  return (
    <a
      href="/auth/login"
      className="text-sm text-accent underline-offset-2 hover:underline"
    >
      Sign in with Google
    </a>
  )
}
