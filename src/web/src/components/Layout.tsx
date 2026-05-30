import type { ReactNode } from 'react'
import { useMe } from '../api/hooks'
import { useUi } from '../store/ui'

export function Layout({ children }: { children: ReactNode }) {
  const { view, setView } = useUi()
  return (
    <div className="min-h-full max-w-5xl mx-auto px-6 py-6">
      <header className="flex items-baseline justify-between mb-4 pb-3 border-b border-rule">
        <h1 className="text-2xl">Longevity</h1>
        <LoginDisplay />
      </header>
      <nav className="flex gap-1 mb-6">
        <NavTab active={view === 'gallery'} onClick={() => setView('gallery')}>
          Gallery
        </NavTab>
        <NavTab active={view === 'vocabulary'} onClick={() => setView('vocabulary')}>
          Vocabulary
        </NavTab>
      </nav>
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
        'px-4 py-1.5 text-sm rounded-sm transition-colors',
        active
          ? 'bg-ink text-paper'
          : 'text-muted hover:text-ink',
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
