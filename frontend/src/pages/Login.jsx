import { useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { apiJson, resetCsrfToken } from '../api/client.js'
import { BusIcon, LockIcon } from '../components/Icons.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Tela de login do passageiro seguindo a referência visual do RotaWeb.
export default function Login({ onUserChange }) {
  // Navegação programática após autenticação.
  const navigate = useNavigate()

  // Localização contém o destino protegido que o usuário tentou abrir antes do login.
  const location = useLocation()

  // Estados controlados do formulário.
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [mostrarSenha, setMostrarSenha] = useState(false)
  const [manterConectado, setManterConectado] = useState(false)

  // Feedback e estado de envio.
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)

  // Envia credenciais apenas ao backend; a senha nunca é persistida no navegador.
  async function submit(event) {
    // Impede o recarregamento padrão do form HTML.
    event.preventDefault()

    // Validação básica melhora UX, mas o servidor repete toda a validação.
    if (!email.trim() || !senha) {
      setMessage('Informe e-mail e senha.')
      return
    }

    // Bloqueia duplo clique durante a chamada.
    setLoading(true)
    setMessage('')

    try {
      // Envia JSON por HTTPS em produção e same-origin no build integrado.
      const { response, data } = await apiJson('/auth/login', 'POST', {
        email: email.trim(),
        senha,
        manterConectado,
      })

      // Erro de autenticação é apresentado de forma genérica.
      if (!response.ok) {
        setMessage(data?.mensagem || 'Não foi possível entrar.')
        return
      }

      // A identidade da sessão mudou. Descarta o token CSRF anônimo usado no login.
      resetCsrfToken()

      // Guarda somente dados de perfil em memória; o cookie HttpOnly já foi emitido pelo servidor.
      onUserChange?.(data.usuario)

      // Retorna ao destino solicitado antes do login ou à busca principal.
      const returnTo = location.state?.returnTo || '/'
      navigate(returnTo, { replace: true })
    } catch {
      // Falha de rede não expõe detalhe interno.
      setMessage('Não foi possível conectar ao servidor.')
    } finally {
      // Reabilita o botão em qualquer resultado.
      setLoading(false)
    }
  }

  // Renderiza o layout em duas colunas documentado no projeto.
  return (
    <div className="auth-page">
      <header className="site-header auth-header">
        {/* Marca do sistema. */}
        <Link className="brand" to="/">
          <span className="brand-icon"><BusIcon size={26} /></span>
          <span><strong>RotaWeb</strong><small>Passagens rodoviárias</small></span>
        </Link>

        {/* Atalhos públicos. */}
        <nav className="main-nav" aria-label="Navegação pública">
          <Link to="/">Viagens</Link>
          <Link to="/minhas-passagens">Minhas passagens</Link>
        </nav>

        {/* Acesso direto evita confundir login de passageiro com login administrativo. */}
        <div className="header-actions">
          <Link className="admin-shortcut" to="/admin/login">Área administrativa</Link>
          <span className="button-secondary is-current">Entrar</span>
        </div>
      </header>

      <div className="auth-content">
        {/* Coluna institucional. */}
        <section className="auth-hero">
          <span className="hero-icon"><BusIcon size={32} /></span>
          <span className="eyebrow">SUA CONTA ROTAWEB</span>
          <h1>O próximo destino<br />está logo ali.</h1>
          <p>Escolha uma viagem, encontre seu assento e tenha suas passagens sempre à mão.</p>
          <ul className="feature-list">
            <li>Suas passagens em um só lugar</li>
            <li>Uma conta para todas as viagens</li>
            <li>Consulta rápida, quando precisar</li>
          </ul>
        </section>

        {/* Cartão de login. */}
        <section className="auth-card">
          <span className="card-icon"><LockIcon /></span>
          <h2>Bem-vindo de volta</h2>
          <p>Entre com seu e-mail e senha do RotaWeb.</p>

          {/* Formulário controlado. */}
          <form className="stack-form" onSubmit={submit} noValidate>
            <label>
              <span>E-mail</span>
              <input
                type="email"
                autoComplete="email"
                placeholder="voce@exemplo.com"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                required
              />
            </label>

            <label>
              <span>Senha</span>
              <div className="password-field">
                <input
                  type={mostrarSenha ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={senha}
                  onChange={(event) => setSenha(event.target.value)}
                  required
                />
                <button type="button" onClick={() => setMostrarSenha((value) => !value)}>
                  {mostrarSenha ? 'Ocultar' : 'Ver'}
                </button>
              </div>
            </label>

            <label className="checkbox-row">
              <input
                type="checkbox"
                checked={manterConectado}
                onChange={(event) => setManterConectado(event.target.checked)}
              />
              <span>Manter conectado neste dispositivo</span>
            </label>

            <StatusMessage type="error">{message}</StatusMessage>

            <button className="button-primary button-block" type="submit" disabled={loading}>
              {loading ? 'Entrando...' : 'Entrar'}
            </button>
          </form>

          {/* Alternância para cadastro e atalho claro para a administração. */}
          <p className="form-switch">Ainda não tem conta? <Link to="/cadastro">Cadastre-se</Link></p>
          <p className="admin-login-switch">É administrador? <Link to="/admin/login">Entrar na administração</Link></p>
        </section>
      </div>
    </div>
  )
}
