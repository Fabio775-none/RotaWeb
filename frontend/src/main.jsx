import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import App from './App.jsx'
import './styles.css'

// Localiza o único ponto de montagem do React dentro do index.html.
const rootElement = document.getElementById('root')

// Falha cedo caso o HTML tenha sido alterado incorretamente.
if (!rootElement) {
  throw new Error('Elemento #root não encontrado.')
}

// Cria a árvore React com StrictMode para detectar padrões problemáticos em desenvolvimento.
createRoot(rootElement).render(
  <StrictMode>
    {/* BrowserRouter mantém URLs reais e permite retorno ao destino solicitado após login. */}
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
