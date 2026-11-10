import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { apiFetch } from '../api/client.js'
import Layout from '../components/Layout.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Página de fechamento da compra: comprovante demonstrativo e cartões de embarque.
export default function CompraConcluida({ user, onUserChange }) {
  const [searchParams] = useSearchParams()
  const [tickets, setTickets] = useState([])
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(true)

  const txid = searchParams.get('txid') || 'RWDEMO-SEM-REFERENCIA'
  const ids = useMemo(
    () => (searchParams.get('ids') || '').split(',').map((value) => value.trim()).filter(Boolean).slice(0, 5),
    [searchParams],
  )

  useEffect(() => {
    let active = true

    async function load() {
      if (ids.length === 0) {
        setMessage('Nenhuma passagem foi informada para este comprovante.')
        setLoading(false)
        return
      }

      try {
        const results = await Promise.all(ids.map((ticketId) => apiFetch(`/tickets/${ticketId}`)))
        const loaded = results.filter((item) => item.response.ok).map((item) => item.data)
        if (active) {
          setTickets(loaded)
          if (loaded.length !== ids.length) setMessage('Algumas passagens não puderam ser carregadas.')
        }
      } catch {
        if (active) setMessage('Não foi possível carregar o comprovante.')
      } finally {
        if (active) setLoading(false)
      }
    }

    load()
    return () => { active = false }
  }, [ids])

  const totalCentavos = tickets.reduce((sum, ticket) => sum + ticket.valorCentavos, 0)
  const money = (cents) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cents / 100)
  const purchaseDate = tickets[0]?.compradoEm ? new Date(tickets[0].compradoEm) : null

  if (loading) {
    return <Layout user={user} onUserChange={onUserChange}><div className="page-state">Gerando comprovante...</div></Layout>
  }

  return (
    <Layout user={user} onUserChange={onUserChange}>
      <section className="content-width section-space purchase-complete-page">
        <StatusMessage type="error">{message}</StatusMessage>

        {tickets.length > 0 && (
          <>
            <div className="panel payment-receipt">
              <div className="section-heading">
                <div>
                  <span className="eyebrow">PAGAMENTO DEMONSTRATIVO</span>
                  <h1>Compra concluída</h1>
                </div>
                <span className="status-pill status-confirmada">Aprovado</span>
              </div>

              <div className="payment-stamp">PAGAMENTO SIMULADO · SEM VALOR FISCAL</div>

              <div className="receipt-grid">
                <div><span>Referência</span><strong>{txid}</strong></div>
                <div><span>Forma</span><strong>Pix demonstrativo</strong></div>
                <div><span>Data</span><strong>{purchaseDate?.toLocaleString('pt-BR') || '-'}</strong></div>
                <div><span>Total</span><strong>{money(totalCentavos)}</strong></div>
                <div><span>Passagens</span><strong>{tickets.length}</strong></div>
                <div><span>Poltronas</span><strong>{tickets.map((ticket) => ticket.assento).join(', ')}</strong></div>
              </div>

              <p className="demo-warning">Este comprovante demonstra o fluxo do sistema acadêmico. Não representa liquidação por instituição financeira.</p>
              <div className="action-row no-print">
                <button className="button-secondary" type="button" onClick={() => window.print()}>Imprimir / salvar PDF</button>
                <Link className="button-secondary" to="/minhas-passagens">Minhas passagens</Link>
              </div>
            </div>

            <div className="boarding-list">
              {tickets.map((ticket) => (
                <article className="boarding-pass" key={ticket.id}>
                  <div className="boarding-pass-main">
                    <div>
                      <span className="eyebrow">CARTÃO DE EMBARQUE</span>
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
                    <div><span>Documento</span><strong>{ticket.tipoDocumento || 'Documento'} · {ticket.documento}</strong></div>
                    <div><span>Nascimento</span><strong>{ticket.dataNascimento ? new Date(`${ticket.dataNascimento}T12:00:00`).toLocaleDateString('pt-BR') : '-'}</strong></div>
                    <div><span>Ônibus</span><strong>{ticket.viagem.onibus.prefixo}</strong></div>
                    <div><span>Código</span><strong>{ticket.codigo}</strong></div>
                  </div>

                  {ticket.responsavelNome && (
                    <div className="minor-board-info">
                      <strong>PASSAGEIRO MENOR · RESPONSÁVEL ACOMPANHANTE</strong>
                      <span>{ticket.responsavelNome} · {ticket.responsavelVinculo} · {ticket.responsavelDocumento}</span>
                      <small>Documentos devem ser conferidos pela empresa no embarque.</small>
                    </div>
                  )}


                  <div className="boarding-verification">
                    <span>Apresente este cartão ao cobrador no embarque</span>
                    <strong>{ticket.codigo}</strong>
                  </div>

                  <div className="action-row no-print">
                    <Link className="button-primary" to={`/passagens/${ticket.id}`}>Abrir passagem</Link>
                  </div>
                </article>
              ))}
            </div>
          </>
        )}
      </section>
    </Layout>
  )
}
