import { Link, NavLink, useNavigate } from 'react-router-dom'
import { apiFetch, resetCsrfToken } from '../api/client.js'
import { BusIcon } from './Icons.jsx'

// Layout principal compartilhado entre páginas públicas e de passageiro.
export default function Layout({ user, onUserChange, children }) {
  // Navegação programática permite voltar ao login após logout.
  const navigate = useNavigate()

  // Logout revoga a sessão no servidor antes de limpar o estado visual.
  async function logout() {
    try {
      // Endpoint protegido e CSRF-safe.
      await apiFetch('/auth/logout', { method: 'POST' })
    } finally {
      // Descarta token antiforgery em memória para iniciar um novo ciclo de sessão.
      resetCsrfToken()
      // Limpa usuário no estado superior mesmo se a rede falhar.
      onUserChange?.(null)
      // Retorna à página inicial pública.
      navigate('/')
    }
  }

  // Renderiza cabeçalho, conteúdo e rodapé consistentes com a identidade visual.
  return (
    <div className="app-shell">
      <header className="site-header">
        {/* Marca clicável leva à busca pública. */}
        <Link className="brand" to="/" aria-label="RotaWeb - início">
          <span className="brand-icon"><BusIcon size={26} /></span>
          <span>
            <strong>RotaWeb</strong>
            <small>Passagens rodoviárias</small>
          </span>
        </Link>

        {/* Navegação principal muda o destaque conforme a rota. */}
        <nav className="main-nav" aria-label="Navegação principal">
          <NavLink to="/">Viagens</NavLink>
          {user && <NavLink to="/minhas-passagens">Minhas passagens</NavLink>}
          {user && <NavLink to="/perfil">Perfil</NavLink>}
        </nav>

        {/* Acesso administrativo aparece apenas quando não há passageiro autenticado. */}
        <div className="header-actions">
          {!user && <Link className="admin-shortcut" to="/admin/login" title="Abrir área administrativa">Admin</Link>}
          {user ? (
            <>
              <span className="user-name">{user.nome}</span>
              <button type="button" className="button-secondary" onClick={logout}>Sair</button>
            </>
          ) : (
            <Link className="button-secondary" to="/login">Entrar</Link>
          )}
        </div>
      </header>

      {/* Cada página fornece seu próprio conteúdo sem duplicar o cabeçalho. */}
      <main>{children}</main>

      {/* Rodapé deixa claro o caráter acadêmico/demonstrativo da compra. */}
      <footer className="site-footer">
        <strong>RotaWeb</strong>
        <span>Projeto de passagens rodoviárias - sem cobrança por cartão ou Pix.</span>
      </footer>
    </div>
  )
}
