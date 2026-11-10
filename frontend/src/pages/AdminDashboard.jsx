import { useEffect, useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { apiFetch, apiJson, apiUrl, apiErrorMessage, resetCsrfToken } from '../api/client.js'
import { BusIcon } from '../components/Icons.jsx'
import Modal from '../components/Modal.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Painel administrativo completo com abas de viagens, ônibus e passagens.
export default function AdminDashboard({ admin, onAdminChange }) {
  // Navegação usada no logout administrativo.
  const navigate = useNavigate()

  // Aba selecionada.
  const [tab, setTab] = useState('trips')

  // Indicadores, listas e mensagens.
  const [summary, setSummary] = useState(null)
  const [buses, setBuses] = useState([])
  const [trips, setTrips] = useState([])
  const [tickets, setTickets] = useState([])
  const [message, setMessage] = useState('')

  // Estado de modais de CRUD.
  const [busModal, setBusModal] = useState(null)
  const [tripModal, setTripModal] = useState(null)

  // Filtros de passagens administrativas.
  const [ticketSearch, setTicketSearch] = useState('')
  const [ticketStatus, setTicketStatus] = useState('')

  // Carrega o painel quando abre e quando uma operação pede refresh.
  async function refreshAll() {
    setMessage('')
    try {
      // Faz chamadas em paralelo para reduzir latência percebida.
      const [summaryResult, busesResult, tripsResult] = await Promise.all([
        apiFetch('/admin/dashboard'),
        apiFetch('/admin/buses'),
        apiFetch('/admin/trips'),
      ])

      // Cada retorno só atualiza seu estado quando autorizado.
      if (summaryResult.response.ok) setSummary(summaryResult.data)
      if (busesResult.response.ok) setBuses(busesResult.data)
      if (tripsResult.response.ok) setTrips(tripsResult.data)

      // Mensagem genérica se qualquer endpoint principal falhar.
      if (!summaryResult.response.ok || !busesResult.response.ok || !tripsResult.response.ok) {
        setMessage('Não foi possível carregar todos os dados administrativos.')
      }
    } catch {
      setMessage('Não foi possível conectar ao servidor administrativo.')
    }
  }

  // Carrega dados principais na abertura.
  useEffect(() => { refreshAll() }, [])

  // Carrega passagens quando a aba correspondente é selecionada ou filtros mudam.
  useEffect(() => {
    if (tab !== 'tickets') return

    let active = true
    async function loadTickets() {
      try {
        const params = new URLSearchParams()
        if (ticketStatus) params.set('status', ticketStatus)
        if (ticketSearch.trim()) params.set('busca', ticketSearch.trim())
        const { response, data } = await apiFetch(`/admin/tickets?${params.toString()}`)
        if (active && response.ok) setTickets(data)
      } catch {
        if (active) setMessage('Não foi possível carregar as passagens.')
      }
    }

    loadTickets()
    return () => { active = false }
  }, [tab, ticketSearch, ticketStatus])

  // Logout administrativo revoga sessão separada.
  async function logout() {
    try {
      await apiFetch('/admin/auth/logout', { method: 'POST' })
    } finally {
      resetCsrfToken()
      onAdminChange?.(null)
      navigate('/admin/login', { replace: true })
    }
  }

  // Desativa ônibus depois de confirmação destrutiva explícita.
  async function deactivateBus(bus) {
    if (!window.confirm(`Desativar o ônibus ${bus.prefixo}?`)) return
    const { response, data } = await apiFetch(`/admin/buses/${bus.id}/deactivate`, { method: 'POST' })
    if (!response.ok) {
      setMessage(apiErrorMessage(data, 'Não foi possível desativar o ônibus.'))
      return
    }
    await refreshAll()
  }

  // Cancela viagem e suas passagens confirmadas depois de confirmação explícita.
  async function cancelTrip(trip) {
    if (!window.confirm(`Cancelar a viagem #${trip.id}? As passagens confirmadas também serão canceladas.`)) return
    const { response, data } = await apiFetch(`/admin/trips/${trip.id}/cancel`, { method: 'POST' })
    if (!response.ok) {
      setMessage(apiErrorMessage(data, 'Não foi possível cancelar a viagem.'))
      return
    }
    await refreshAll()
  }

  // Exclui fisicamente uma viagem somente quando não existe nenhuma passagem vinculada.
  async function deleteTrip(trip) {
    if (!window.confirm(`Excluir definitivamente a viagem #${trip.id}?`)) return
    try {
      const { response, data } = await apiFetch(`/admin/trips/${trip.id}`, { method: 'DELETE' })
      if (!response.ok) {
        setMessage(apiErrorMessage(data, 'Não foi possível excluir a viagem.'))
        return
      }
      await refreshAll()
    } catch (error) {
      setMessage(error?.message || 'Não foi possível excluir a viagem.')
    }
  }

  // Exclui um ônibus somente quando ele não possui viagens históricas/futuras vinculadas.
  async function deleteBus(bus) {
    if (!window.confirm(`Excluir definitivamente o ônibus ${bus.prefixo}?`)) return
    try {
      const { response, data } = await apiFetch(`/admin/buses/${bus.id}`, { method: 'DELETE' })
      if (!response.ok) {
        setMessage(apiErrorMessage(data, 'Não foi possível excluir o ônibus.'))
        return
      }
      await refreshAll()
    } catch (error) {
      setMessage(error?.message || 'Não foi possível excluir o ônibus.')
    }
  }

  // Faz download autenticado do CSV usando fetch para preservar cookies de sessão.
  async function exportCsv() {
    const params = new URLSearchParams()
    if (ticketStatus) params.set('status', ticketStatus)
    if (ticketSearch.trim()) params.set('busca', ticketSearch.trim())

    const response = await fetch(apiUrl(`/admin/tickets/export?${params.toString()}`), {
      credentials: 'include',
    })

    if (!response.ok) {
      setMessage('Não foi possível exportar o CSV.')
      return
    }

    // Cria URL temporária somente para o arquivo recebido.
    const blob = await response.blob()
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = 'rotaweb-passagens.csv'
    anchor.click()
    URL.revokeObjectURL(url)
  }

  // Formata moeda.
  const money = (cents) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format((cents || 0) / 100)

  // Renderiza o painel e os modais de formulário.
  return (
    <div className="admin-shell">
      <header className="admin-header">
        <div className="brand">
          <span className="brand-icon"><BusIcon size={26} /></span>
          <span><strong>RotaWeb</strong><small>Administração</small></span>
        </div>
        <div className="header-actions"><span className="user-name">{admin?.nome}</span><button className="button-secondary" onClick={logout}>Sair</button></div>
      </header>

      <main className="admin-content">
        <div className="section-heading">
          <div><span className="eyebrow">PAINEL ADMINISTRATIVO</span><h1>Operação RotaWeb</h1></div>
        </div>

        <StatusMessage type="error">{message}</StatusMessage>

        {/* Indicadores resumidos. */}
        <div className="metric-grid">
          <Metric label="Ônibus ativos" value={summary?.activeBuses ?? '-'} />
          <Metric label="Viagens futuras" value={summary?.futureTrips ?? '-'} />
          <Metric label="Passagens confirmadas" value={summary?.confirmedTickets ?? '-'} />
          <Metric label="Valor confirmado" value={summary ? money(summary.confirmedValueCents) : '-'} />
        </div>

        {/* Abas do painel. */}
        <div className="admin-tabs">
          <button className={tab === 'trips' ? 'active' : ''} onClick={() => setTab('trips')}>Viagens</button>
          <button className={tab === 'buses' ? 'active' : ''} onClick={() => setTab('buses')}>Ônibus</button>
          <button className={tab === 'tickets' ? 'active' : ''} onClick={() => setTab('tickets')}>Passagens</button>
        </div>

        {/* Conteúdo da aba de viagens. */}
        {tab === 'trips' && (
          <section className="admin-section">
            <div className="section-heading"><h2>Viagens</h2><button className="button-primary" onClick={() => setTripModal({ mode: 'create' })}>Cadastrar viagem</button></div>
            <div className="table-wrap">
              <table><thead><tr><th>ID</th><th>Rota</th><th>Partida</th><th>Veículo</th><th>Preço</th><th>Ocupação</th><th>Status</th><th>Ações</th></tr></thead><tbody>
                {trips.map((trip) => (
                  <tr key={trip.id}><td>{trip.id}</td><td>{trip.origem.nome}/{trip.origem.uf} → {trip.destino.nome}/{trip.destino.uf}</td><td>{new Date(trip.partida).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })}</td><td>{trip.onibus.prefixo}</td><td>{money(trip.precoCentavos)}</td><td>{trip.ocupados}/{trip.assentos}</td><td>{trip.status}</td><td><div className="table-actions"><button className="button-secondary button-small" onClick={() => setTripModal({ mode: 'edit', trip })} disabled={trip.ocupados > 0 || trip.status !== 'Programada'}>Editar</button><button className="button-danger button-small" onClick={() => cancelTrip(trip)} disabled={trip.status !== 'Programada'}>Cancelar</button><button className="button-danger button-small" onClick={() => deleteTrip(trip)}>Excluir</button></div></td></tr>
                ))}
              </tbody></table>
            </div>
          </section>
        )}

        {/* Conteúdo da aba de ônibus. */}
        {tab === 'buses' && (
          <section className="admin-section">
            <div className="section-heading"><h2>Frota</h2><button className="button-primary" onClick={() => setBusModal({ mode: 'create' })}>Cadastrar ônibus</button></div>
            <div className="table-wrap">
              <table><thead><tr><th>Prefixo</th><th>Placa</th><th>Modelo</th><th>Categoria</th><th>Assentos</th><th>Situação</th><th>Ações</th></tr></thead><tbody>
                {buses.map((bus) => (
                  <tr key={bus.id}><td>{bus.prefixo}</td><td>{bus.placa}</td><td>{bus.modelo}</td><td>{bus.categoria}</td><td>{bus.assentos}</td><td>{bus.ativo ? 'Ativo' : 'Inativo'}</td><td><div className="table-actions"><button className="button-secondary button-small" onClick={() => setBusModal({ mode: 'edit', bus })}>Editar</button>{bus.ativo && <button className="button-danger button-small" onClick={() => deactivateBus(bus)}>Desativar</button>}<button className="button-danger button-small" onClick={() => deleteBus(bus)}>Excluir</button></div></td></tr>
                ))}
              </tbody></table>
            </div>
          </section>
        )}

        {/* Conteúdo da aba de passagens. */}
        {tab === 'tickets' && (
          <section className="admin-section">
            <div className="section-heading"><h2>Passagens</h2><button className="button-secondary" onClick={exportCsv}>Exportar CSV</button></div>
            <div className="admin-filters"><input placeholder="Código, passageiro ou documento" value={ticketSearch} onChange={(event) => setTicketSearch(event.target.value)} /><select value={ticketStatus} onChange={(event) => setTicketStatus(event.target.value)}><option value="">Todos os status</option><option>Confirmada</option><option>Cancelada</option></select></div>
            <div className="table-wrap">
              <table><thead><tr><th>Código</th><th>Passageiro</th><th>Viagem</th><th>Poltrona</th><th>Valor</th><th>Status</th></tr></thead><tbody>
                {tickets.map((ticket) => <tr key={ticket.id}><td>{ticket.codigo}</td><td>{ticket.passageiro}</td><td>#{ticket.viagem.id} {ticket.viagem.origem.nome} → {ticket.viagem.destino.nome}</td><td>{ticket.assento}</td><td>{money(ticket.valorCentavos)}</td><td>{ticket.status}</td></tr>)}
              </tbody></table>
            </div>
          </section>
        )}
      </main>

      {/* Modal de ônibus. */}
      {busModal && <BusFormModal state={busModal} onClose={() => setBusModal(null)} onSaved={async () => { setBusModal(null); await refreshAll() }} />}

      {/* Modal de viagem. */}
      {tripModal && <TripFormModal state={tripModal} buses={buses.filter((bus) => bus.ativo)} onClose={() => setTripModal(null)} onSaved={async () => { setTripModal(null); await refreshAll() }} />}
    </div>
  )
}

// Cartão de indicador do painel.
function Metric({ label, value }) {
  return <div className="metric-card"><span>{label}</span><strong>{value}</strong></div>
}

// Modal de criação/edição de ônibus.
function BusFormModal({ state, onClose, onSaved }) {
  // Usa dados existentes no modo de edição ou valores padrão no cadastro.
  const source = state.bus || {}
  const [form, setForm] = useState({ prefixo: source.prefixo || '', placa: source.placa || '', modelo: source.modelo || '', categoria: source.categoria || 'Executivo', assentos: source.assentos || 36 })
  const [message, setMessage] = useState('')

  // Atualiza um único campo sem perder os demais.
  function setField(name, value) { setForm((current) => ({ ...current, [name]: value })) }

  // Salva via POST ou PUT conforme o modo.
  async function submit(event) {
    event.preventDefault()
    setMessage('')

    if (!form.prefixo.trim() || !form.placa.trim() || !form.modelo.trim()) {
      setMessage('Preencha prefixo, placa e modelo.')
      return
    }

    const path = state.mode === 'edit' ? `/admin/buses/${source.id}` : '/admin/buses'
    const method = state.mode === 'edit' ? 'PUT' : 'POST'

    try {
      const { response, data } = await apiJson(path, method, { ...form, assentos: Number(form.assentos) })
      if (!response.ok) {
        setMessage(apiErrorMessage(data, 'Não foi possível salvar o ônibus.'))
        return
      }
      await onSaved()
    } catch (error) {
      setMessage(error?.message || 'Não foi possível salvar o ônibus.')
    }
  }

  // Renderiza formulário em modal.
  return (
    <Modal title={state.mode === 'edit' ? 'Editar ônibus' : 'Cadastrar ônibus'} onClose={onClose}>
      <form className="stack-form" onSubmit={submit}>
        <label><span>Prefixo</span><input value={form.prefixo} onChange={(event) => setField('prefixo', event.target.value)} maxLength={12} /></label>
        <label><span>Placa</span><input value={form.placa} onChange={(event) => setField('placa', event.target.value.toUpperCase())} maxLength={7} /></label>
        <label><span>Modelo</span><input value={form.modelo} onChange={(event) => setField('modelo', event.target.value)} maxLength={80} /></label>
        <label><span>Categoria</span><select value={form.categoria} onChange={(event) => setField('categoria', event.target.value)}><option>Convencional</option><option>Executivo</option><option>Semi-leito</option><option>Leito</option></select></label>
        <label><span>Assentos</span><input type="number" min="16" max="60" value={form.assentos} onChange={(event) => setField('assentos', event.target.value)} /></label>
        <StatusMessage type="error">{message}</StatusMessage>
        <button className="button-primary">Salvar</button>
      </form>
    </Modal>
  )
}

// Modal de criação/edição de viagem com localidades oficiais do IBGE.
function TripFormModal({ state, buses, onClose, onSaved }) {
  // Dados da viagem existente quando editando.
  const source = state.trip || null

  // Estados/cidades do IBGE.
  const [states, setStates] = useState([])
  const [originState, setOriginState] = useState(source?.origem?.uf || 'SP')
  const [destinationState, setDestinationState] = useState(source?.destino?.uf || 'RJ')
  const [originCities, setOriginCities] = useState([])
  const [destinationCities, setDestinationCities] = useState([])

  // Formulário usa valores da viagem ou padrões vazios.
  const [form, setForm] = useState({
    onibusId: source?.onibus?.id || buses[0]?.id || '',
    origemId: source?.origem?.id || '',
    destinoId: source?.destino?.id || '',
    partida: source ? toLocalInput(source.partida) : '',
    chegada: source ? toLocalInput(source.chegada) : '',
    preco: source ? (source.precoCentavos / 100).toFixed(2) : '',
  })
  const [message, setMessage] = useState('')

  // Atualiza campo individual.
  function setField(name, value) { setForm((current) => ({ ...current, [name]: value })) }

  // Carrega estados na abertura.
  useEffect(() => { apiFetch('/locations/states').then(({ response, data }) => { if (response.ok) setStates(data) }) }, [])

  // Carrega cidades de origem.
  useEffect(() => {
    apiFetch(`/locations/states/${originState}/cities`).then(({ response, data }) => {
      if (!response.ok) return
      setOriginCities(data)
      if (!data.some((city) => String(city.id) === String(form.origemId))) setField('origemId', data[0]?.id || '')
    })
  }, [originState])

  // Carrega cidades de destino.
  useEffect(() => {
    apiFetch(`/locations/states/${destinationState}/cities`).then(({ response, data }) => {
      if (!response.ok) return
      setDestinationCities(data)
      if (!data.some((city) => String(city.id) === String(form.destinoId))) setField('destinoId', data[0]?.id || '')
    })
  }, [destinationState])

  // Salva viagem e converte preço de reais para centavos de forma determinística.
  async function submit(event) {
    event.preventDefault()
    setMessage('')

    const departure = new Date(form.partida)
    const arrival = new Date(form.chegada)
    const priceCents = Math.round(Number(String(form.preco).replace(',', '.')) * 100)

    if (!form.onibusId || !form.origemId || !form.destinoId || Number(form.origemId) === Number(form.destinoId)) {
      setMessage('Escolha ônibus, origem e destino diferentes.')
      return
    }
    if (Number.isNaN(departure.getTime()) || Number.isNaN(arrival.getTime())) {
      setMessage('Informe data e hora de partida e chegada.')
      return
    }
    if (arrival <= departure) {
      setMessage('A chegada precisa ser posterior à partida.')
      return
    }
    if (arrival.getTime() - departure.getTime() > 7 * 24 * 60 * 60 * 1000) {
      setMessage('A duração máxima permitida para uma viagem é de 7 dias.')
      return
    }
    if (!Number.isFinite(priceCents) || priceCents <= 0) {
      setMessage('Informe um preço válido maior que zero.')
      return
    }

    const body = {
      onibusId: Number(form.onibusId),
      origemId: Number(form.origemId),
      destinoId: Number(form.destinoId),
      partida: departure.toISOString(),
      chegada: arrival.toISOString(),
      precoCentavos: priceCents,
    }
    const path = state.mode === 'edit' ? `/admin/trips/${source.id}` : '/admin/trips'
    const method = state.mode === 'edit' ? 'PUT' : 'POST'

    try {
      const { response, data } = await apiJson(path, method, body)
      if (!response.ok) {
        setMessage(apiErrorMessage(data, 'Não foi possível salvar a viagem.'))
        return
      }
      await onSaved()
    } catch (error) {
      setMessage(error?.message || 'Não foi possível salvar a viagem.')
    }
  }

  // Formulário administrativo de viagem.
  return (
    <Modal title={state.mode === 'edit' ? 'Editar viagem' : 'Cadastrar viagem'} onClose={onClose}>
      <form className="stack-form" onSubmit={submit}>
        <label><span>Ônibus</span><select value={form.onibusId} onChange={(event) => setField('onibusId', event.target.value)}>{buses.map((bus) => <option key={bus.id} value={bus.id}>{bus.prefixo} - {bus.modelo}</option>)}</select></label>
        <div className="form-two-columns"><label><span>UF origem</span><select value={originState} onChange={(event) => setOriginState(event.target.value)}>{states.map((state) => <option key={state.sigla}>{state.sigla}</option>)}</select></label><label><span>Cidade origem</span><select value={form.origemId} onChange={(event) => setField('origemId', event.target.value)}>{originCities.map((city) => <option key={city.id} value={city.id}>{city.nome}</option>)}</select></label></div>
        <div className="form-two-columns"><label><span>UF destino</span><select value={destinationState} onChange={(event) => setDestinationState(event.target.value)}>{states.map((state) => <option key={state.sigla}>{state.sigla}</option>)}</select></label><label><span>Cidade destino</span><select value={form.destinoId} onChange={(event) => setField('destinoId', event.target.value)}>{destinationCities.map((city) => <option key={city.id} value={city.id}>{city.nome}</option>)}</select></label></div>
        <div className="form-two-columns"><label><span>Partida</span><input type="datetime-local" value={form.partida} onChange={(event) => setField('partida', event.target.value)} /></label><label><span>Chegada</span><input type="datetime-local" value={form.chegada} onChange={(event) => setField('chegada', event.target.value)} /></label></div>
        <label><span>Preço (R$)</span><input inputMode="decimal" value={form.preco} onChange={(event) => setField('preco', event.target.value)} /></label>
        <StatusMessage type="error">{message}</StatusMessage>
        <button className="button-primary">Salvar viagem</button>
      </form>
    </Modal>
  )
}

// Converte ISO/UTC para valor aceito por datetime-local usando o horário local do navegador.
function toLocalInput(value) {
  // Data inválida gera campo vazio em vez de exceção visual.
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return ''

  // Compensa o offset local antes de cortar o ISO sem sufixo Z.
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
  return local.toISOString().slice(0, 16)
}
