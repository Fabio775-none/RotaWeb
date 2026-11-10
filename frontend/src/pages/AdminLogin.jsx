import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiJson, resetCsrfToken } from '../api/client.js'
import { BusIcon, LockIcon } from '../components/Icons.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Login administrativo separado dos passageiros, somente por e-mail e senha.
export default function AdminLogin() {
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [senha, setSenha] = useState('')
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)

  async function submit(event) {
    event.preventDefault()
    setLoading(true)
    setMessage('')
    try {
      resetCsrfToken()
      const { response, data } = await apiJson('/admin/auth/login', 'POST', {
        email: email.trim(), senha,
      })
      if (!response.ok) {
        setMessage(data?.mensagem || 'Não foi possível autenticar.')
        return
      }
      resetCsrfToken() // A identidade da sessão mudou após o login.
      navigate('/admin', { replace: true })
    } catch {
      setMessage('Não foi possível conectar ao servidor.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="admin-login-page">
      <section className="admin-login-card">
        <Link className="brand" to="/">
          <span className="brand-icon"><BusIcon size={26} /></span>
          <span><strong>RotaWeb</strong><small>Administração</small></span>
        </Link>
        <span className="card-icon admin-lock"><LockIcon /></span>
        <span className="eyebrow">ACESSO RESTRITO</span>
        <h1>Administração</h1>
        <p>Entre com seu e-mail e senha para acessar o painel administrativo.</p>
        <form className="stack-form" onSubmit={submit}>
          <label><span>E-mail administrativo</span><input type="email" required autoComplete="username" value={email} onChange={(event) => setEmail(event.target.value)} /></label>
          <label><span>Senha</span><input type="password" required autoComplete="current-password" value={senha} onChange={(event) => setSenha(event.target.value)} /></label>
          <StatusMessage type="error">{message}</StatusMessage>
          <button className="button-primary button-block" disabled={loading}>{loading ? 'Entrando...' : 'Entrar'}</button>
        </form>
        <Link className="admin-back" to="/">Voltar ao site</Link>
      </section>
    </div>
  )
}
