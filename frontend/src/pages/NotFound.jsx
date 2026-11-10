import { Link } from 'react-router-dom'

// Página 404 controlada para rotas inexistentes.
export default function NotFound() {
  // Mantém uma saída clara sem revelar detalhes de roteamento interno.
  return (
    <div className="not-found">
      <span className="eyebrow">404</span>
      <h1>Página não encontrada</h1>
      <p>O endereço informado não faz parte do RotaWeb.</p>
      <Link className="button-primary" to="/">Voltar à busca</Link>
    </div>
  )
}
