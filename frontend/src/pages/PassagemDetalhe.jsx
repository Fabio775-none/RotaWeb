import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { apiFetch } from '../api/client.js'
import Layout from '../components/Layout.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Detalhe individual com comprovante e cartão de embarque.
export default function PassagemDetalhe({ user, onUserChange }) {
  const { id } = useParams()
  const [ticket, setTicket] = useState(null)
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(true)

  async function load() {
    setLoading(true)
    try {
      const { response, data } = await apiFetch(`/tickets/${id}`)
      if (response.ok) setTicket(data)
      else setMessage(data?.mensagem || 'Passagem não encontrada.')
    } catch {
      setMessage('Não foi possível conectar ao servidor.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { load() }, [id])

  async function cancel() {
    if (!window.confirm('Deseja realmente cancelar esta passagem?')) return

    setMessage('')
    const { response, data } = await apiFetch(`/tickets/${id}/cancel`, { method: 'POST' })
    if (!response.ok) {
      setMessage(data?.mensagem || 'Não foi possível cancelar.')
      return
    }
    setMessage('Passagem cancelada com sucesso.')
    await load()
  }

  if (loading && !ticket) {
    return <Layout user={user} onUserChange={onUserChange}><div className="page-state">Carregando comprovante...</div></Layout>
  }

  if (!ticket) {
    return <Layout user={user} onUserChange={onUserChange}><div className="content-width section-space"><StatusMessage type="error">{message}</StatusMessage></div></Layout>
  }

  const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(ticket.valorCentavos / 100)
  const canCancel = ticket.status === 'Confirmada' && new Date(ticket.viagem.partida) > new Date()

  return (
    <Layout user={user} onUserChange={onUserChange}>
      <section className="content-width section-space">
        <div className="receipt panel">
          <div className="section-heading">
            <div><span className="eyebrow">PASSAGEM</span><h1>{ticket.codigo}</h1></div>
            <span className={`status-pill status-${ticket.status.toLowerCase()}`}>{ticket.status}</span>
          </div>

          <div className="receipt-grid">
            <div><span>Origem</span><strong>{ticket.viagem.origem.nome}/{ticket.viagem.origem.uf}</strong></div>
            <div><span>Destino</span><strong>{ticket.viagem.destino.nome}/{ticket.viagem.destino.uf}</strong></div>
            <div><span>Partida</span><strong>{new Date(ticket.viagem.partida).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })}</strong></div>
            <div><span>Poltrona</span><strong>{ticket.assento}</strong></div>
            <div><span>Passageiro</span><strong>{ticket.passageiro}</strong></div>
            <div><span>Nascimento</span><strong>{ticket.dataNascimento ? new Date(`${ticket.dataNascimento}T12:00:00`).toLocaleDateString('pt-BR') : '-'}</strong></div>
            <div><span>Documento</span><strong>{ticket.tipoDocumento || 'Documento'} · {ticket.documento}</strong></div>
            <div><span>Ônibus</span><strong>{ticket.viagem.onibus.prefixo} - {ticket.viagem.onibus.modelo}</strong></div>
            <div><span>Valor</span><strong>{money}</strong></div>
          </div>

          {ticket.responsavelNome && (
            <div className="minor-board-info">
              <strong>PASSAGEIRO MENOR · RESPONSÁVEL ACOMPANHANTE</strong>
              <span>{ticket.responsavelNome} · {ticket.responsavelVinculo} · {ticket.responsavelDocumento}</span>
              <small>Documentos do menor e do responsável devem ser apresentados para conferência no embarque.</small>
            </div>
          )}

          <div className={`boarding-pass boarding-pass-detail ${ticket.status !== 'Confirmada' ? 'boarding-pass-disabled' : ''}`}>
            <div className="boarding-pass-main">
              <div>
                <span className="eyebrow">CARTÃO PARA EMBARQUE</span>
                <span className="boarding-reserved-badge">POLTRONA RESERVADA</span>
                <h2>{ticket.viagem.origem.nome} → {ticket.viagem.destino.nome}</h2>
                <p>{new Date(ticket.viagem.partida).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })}</p>
              </div>
              <div className="boarding-seat">
                <span>POLTRONA</span>
                <strong>{ticket.assento}</strong>
              </div>
            </div>
            <div className="boarding-meta">
              <div><span>Passageiro</span><strong>{ticket.passageiro}</strong></div>
              <div><span>Ônibus</span><strong>{ticket.viagem.onibus.prefixo}</strong></div>
              <div><span>Status</span><strong>{ticket.status}</strong></div>
              <div><span>Código de conferência</span><strong>{ticket.codigo}</strong></div>
            </div>
            <div className="boarding-verification">
              <span>Mostre ao cobrador para conferência</span>
              <strong>{ticket.codigo}</strong>
            </div>
          </div>

          <p className="muted">Bilhete demonstrativo do projeto RotaWeb. Não equivale a documento fiscal.</p>
          <StatusMessage type={message.includes('sucesso') ? 'success' : 'error'}>{message}</StatusMessage>
          <div className="action-row no-print">
            <button className="button-secondary" type="button" onClick={() => window.print()}>Imprimir / salvar PDF</button>
            {canCancel && <button className="button-danger" type="button" onClick={cancel}>Cancelar passagem</button>}
            <Link className="button-secondary" to="/minhas-passagens">Voltar</Link>
          </div>
        </div>
      </section>
    </Layout>
  )
}
