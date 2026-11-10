import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { apiJson } from '../api/client.js'
import { BusIcon, UserIcon } from '../components/Icons.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

function calculateAge(dateString, reference = new Date()) {
  if (!dateString) return -1
  const [year, month, day] = dateString.split('-').map(Number)
  let age = reference.getFullYear() - year
  const beforeBirthday = (reference.getMonth() + 1 < month) ||
    (reference.getMonth() + 1 === month && reference.getDate() < day)
  if (beforeBirthday) age--
  return age
}

// Tela de cadastro público. A conta compradora é exclusiva para pessoas com 18 anos ou mais.
export default function Cadastro() {
  const navigate = useNavigate()

  const [nome, setNome] = useState('')
  const [email, setEmail] = useState('')
  const [dataNascimento, setDataNascimento] = useState('')
  const [senha, setSenha] = useState('')
  const [confirmarSenha, setConfirmarSenha] = useState('')
  const [mostrarSenha, setMostrarSenha] = useState(false)

  const [message, setMessage] = useState('')
  const [type, setType] = useState('error')
  const [loading, setLoading] = useState(false)

  async function submit(event) {
    event.preventDefault()

    if (calculateAge(dataNascimento) < 18) {
      setType('error')
      setMessage('A conta compradora deve pertencer a uma pessoa com 18 anos ou mais.')
      return
    }

    if (senha !== confirmarSenha) {
      setType('error')
      setMessage('As senhas não são iguais.')
      return
    }

    setLoading(true)
    setMessage('')

    try {
      const { response, data } = await apiJson('/auth/register', 'POST', {
        nome: nome.trim(),
        email: email.trim(),
        dataNascimento,
        senha,
        confirmarSenha,
      })

      if (!response.ok) {
        setType('error')
        const details = Array.isArray(data?.erros) ? ` ${data.erros.join(' ')}` : ''
        setMessage(`${data?.mensagem || 'Não foi possível criar a conta.'}${details}`)
        return
      }

      setType('success')
      setMessage('Conta criada com sucesso. Você já pode entrar.')
      setTimeout(() => navigate('/login'), 900)
    } catch {
      setType('error')
      setMessage('Não foi possível conectar ao servidor.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="auth-page">
      <header className="site-header auth-header">
        <Link className="brand" to="/">
          <span className="brand-icon"><BusIcon size={26} /></span>
          <span><strong>RotaWeb</strong><small>Passagens rodoviárias</small></span>
        </Link>
        <nav className="main-nav" aria-label="Navegação pública">
          <Link to="/">Viagens</Link>
          <Link to="/minhas-passagens">Minhas passagens</Link>
        </nav>
        <div className="header-actions">
          <Link className="admin-shortcut" to="/admin/login">Admin</Link>
          <Link className="button-secondary" to="/login">Entrar</Link>
        </div>
      </header>

      <div className="auth-content">
        <section className="auth-hero">
          <span className="hero-icon"><BusIcon size={32} /></span>
          <span className="eyebrow">SUA CONTA ROTAWEB</span>
          <h1>Sua próxima viagem<br />começa aqui.</h1>
          <p>Crie sua conta para escolher assentos, acompanhar passagens e consultar seu histórico.</p>
        </section>

        <section className="auth-card auth-card-tall">
          <span className="card-icon"><UserIcon /></span>
          <h2>Crie sua conta</h2>
          <p>A conta compradora é exclusiva para maiores de 18 anos. Passageiros menores podem viajar com responsável.</p>

          <form className="stack-form" onSubmit={submit} noValidate>
            <label>
              <span>Nome</span>
              <input type="text" autoComplete="name" value={nome} onChange={(event) => setNome(event.target.value)} minLength={3} maxLength={100} required />
            </label>

            <label>
              <span>E-mail</span>
              <input type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} required />
            </label>

            <label>
              <span>Data de nascimento</span>
              <input type="date" value={dataNascimento} onChange={(event) => setDataNascimento(event.target.value)} required />
            </label>
            <p className="field-help">O sistema confere a maioridade também no servidor.</p>

            <label>
              <span>Senha</span>
              <div className="password-field">
                <input type={mostrarSenha ? 'text' : 'password'} autoComplete="new-password" value={senha} onChange={(event) => setSenha(event.target.value)} minLength={10} required />
                <button type="button" onClick={() => setMostrarSenha((value) => !value)}>{mostrarSenha ? 'Ocultar' : 'Ver'}</button>
              </div>
            </label>

            <label>
              <span>Confirmar senha</span>
              <input type={mostrarSenha ? 'text' : 'password'} autoComplete="new-password" value={confirmarSenha} onChange={(event) => setConfirmarSenha(event.target.value)} minLength={10} required />
            </label>

            <p className="field-help">Use pelo menos 10 caracteres, com maiúscula, minúscula, número e variedade de caracteres.</p>

            <StatusMessage type={type}>{message}</StatusMessage>

            <button className="button-primary button-block" type="submit" disabled={loading}>
              {loading ? 'Criando conta...' : 'Criar conta'}
            </button>
          </form>

          <p className="form-switch">Já possui uma conta? <Link to="/login">Entrar</Link></p>
        </section>
      </div>
    </div>
  )
}
