import { Layout } from './components/Layout'
import { Home } from './pages/Home'
import { Vocabulary } from './pages/Vocabulary'
import { useUi } from './store/ui'

export function App() {
  const view = useUi((s) => s.view)
  return (
    <Layout>
      {view === 'vocabulary' ? <Vocabulary /> : <Home />}
    </Layout>
  )
}
