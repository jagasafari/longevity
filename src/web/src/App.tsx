import { Layout } from './components/Layout'
import { GalleryFilters } from './components/GalleryFilters'
import { Home } from './pages/Home'
import { Vocabulary } from './pages/Vocabulary'
import { useUi } from './store/ui'

export function App() {
  const view = useUi((s) => s.view)
  return (
    <Layout controls={view === 'gallery' ? <GalleryFilters /> : undefined}>
      {view === 'vocabulary' ? <Vocabulary /> : <Home />}
    </Layout>
  )
}
