import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api/client.js'
import Layout from '../components/Layout.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Histórico de passagens da conta autenticada.
export default function MinhasPassagens({ user, onUserChange }) {
  // Filtro visual de status.
  const [filter, setFilter] = useState('Todas')

  // Lista e feedback.
  const [tickets, setTickets] = useState([])
  const [loading, setLoading] = useState(true)
  const [message, setMessage] = useState('')

  // Carrega novamente quando o filtro muda.
  useEffect(() => {
    let active = true

    async function load() {
      setLoading(true)
      setMessage('')
      try {
        // O backend sempre filtra pelo usuário da sessão, impedindo IDOR.
        const query = filter === 'Todas' ? '' : `?status=${encodeURIComponent(filter)}`
        const { response, data } = await apiFetch(`/tickets/mine${query}`)
        if (active && response.ok) {
          setTickets(data)
        } else if (active) {
          setMessage(data?.mensagem || 'Não foi possível consultar suas passagens.')
        }
      } catch {
        if (active) setMessage('Não foi possível conectar ao servidor.')
      } finally {
        if (active) setLoading(false)
      }
    }

    load()
    return () => { active = false }
  }, [filter])

  // Formata valores em centavos.
  const money = (cents) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cents / 100)

  // Renderiza filtros e cartões.
  return (
    <Layout user={user} onUserChange={onUserChange}>
      <section className="content-width section-space">
        <div className="section-heading">
          <div><span className="eyebrow">MINHAS PASSAGENS</span><h1>Seu histórico de viagens</h1></div>
          <Link className="button-primary" to="/">Buscar nova viagem</Link>
        </div>

        <div className="filter-tabs">
          {['Todas', 'Confirmada', 'Cancelada'].map((item) => (
            <button key={item} className={filter === item ? 'active' : ''} type="button" onClick={() => setFilter(item)}>{item}</button>
          ))}
        </div>

        <StatusMessage type="error">{message}</StatusMessage>
        {loading && <div className="page-state">Carregando passagens...</div>}

        <div className="ticket-list">
          {tickets.map((ticket) => (
            <article className="ticket-card" key={ticket.id}>
              <div><span className="eyebrow">{ticket.codigo}</span><h2>{ticket.viagem.origem.nome} → {ticket.viagem.destino.nome}</h2><p>{new Date(ticket.viagem.partida).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })}</p></div>
              <div><span>Poltrona</span><strong>{ticket.assento}</strong></div>
              <div><span>Passageiro</span><strong>{ticket.passageiro}</strong></div>
              <div><span>Valor</span><strong>{money(ticket.valorCentavos)}</strong></div>
              <span className={`status-pill status-${ticket.status.toLowerCase()}`}>{ticket.status}</span>
              <Link className="button-secondary" to={`/passagens/${ticket.id}`}>Abrir passagem</Link>
            </article>
          ))}
        </div>

        {!loading && tickets.length === 0 && !message && <div className="empty-state">Nenhuma passagem encontrada neste filtro.</div>}
      </section>
    </Layout>
  )
}
