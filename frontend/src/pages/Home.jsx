import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiFetch } from '../api/client.js'
import Layout from '../components/Layout.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

// Cidades de contingência da demonstração. O backend devolve a mesma lista a partir do banco.
const FALLBACK_CITIES = [
  { id: 3106200, nome: 'Belo Horizonte', uf: 'MG' },
  { id: 3170206, nome: 'Uberlândia', uf: 'MG' },
  { id: 4106902, nome: 'Curitiba', uf: 'PR' },
  { id: 4113700, nome: 'Londrina', uf: 'PR' },
  { id: 3304557, nome: 'Rio de Janeiro', uf: 'RJ' },
  { id: 3303302, nome: 'Niterói', uf: 'RJ' },
  { id: 3550308, nome: 'São Paulo', uf: 'SP' },
  { id: 3509502, nome: 'Campinas', uf: 'SP' },
]

const STATE_NAMES = {
  MG: 'Minas Gerais',
  PR: 'Paraná',
  RJ: 'Rio de Janeiro',
  SP: 'São Paulo',
}

// Página pública de busca e resultados de viagens.
export default function Home({ user, onUserChange }) {
  // A busca principal mostra somente cidades que têm viagens de demonstração cadastradas.
  // A integração completa com municípios oficiais do IBGE continua disponível no backend/admin.
  const [availableCities, setAvailableCities] = useState(FALLBACK_CITIES)
  const [originState, setOriginState] = useState('SP')
  const [destinationState, setDestinationState] = useState('RJ')
  const [originId, setOriginId] = useState('3550308')
  const [destinationId, setDestinationId] = useState('3304557')
  const [date, setDate] = useState(() => {
    const tomorrow = new Date()
    tomorrow.setDate(tomorrow.getDate() + 1)
    return tomorrow.toISOString().slice(0, 10)
  })
  const [category, setCategory] = useState('Todas')
  const [order, setOrder] = useState('horario')
  const [trips, setTrips] = useState([])
  const [loading, setLoading] = useState(false)
  const [message, setMessage] = useState('')

  // Busca as cidades que realmente aparecem em viagens futuras.
  useEffect(() => {
    let active = true

    async function loadAvailableCities() {
      try {
        const { response, data } = await apiFetch('/locations/available')
        if (active && response.ok && Array.isArray(data) && data.length > 0) {
          setAvailableCities(data)
        }
      } catch {
        // A lista local mantém a apresentação utilizável mesmo sem a consulta remota.
      }
    }

    loadAvailableCities()
    return () => { active = false }
  }, [])

  const states = useMemo(() => {
    const ufs = [...new Set(availableCities.map((city) => city.uf))].sort()
    return ufs.map((uf) => ({ sigla: uf, nome: STATE_NAMES[uf] || uf }))
  }, [availableCities])

  const originCities = useMemo(
    () => availableCities.filter((city) => city.uf === originState).sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')),
    [availableCities, originState],
  )

  const destinationCities = useMemo(
    () => availableCities.filter((city) => city.uf === destinationState).sort((a, b) => a.nome.localeCompare(b.nome, 'pt-BR')),
    [availableCities, destinationState],
  )

  // Mantém um município válido sempre que a UF muda.
  useEffect(() => {
    if (!originCities.some((city) => String(city.id) === String(originId))) {
      setOriginId(originCities[0] ? String(originCities[0].id) : '')
    }
  }, [originCities, originId])

  useEffect(() => {
    if (!destinationCities.some((city) => String(city.id) === String(destinationId))) {
      setDestinationId(destinationCities[0] ? String(destinationCities[0].id) : '')
    }
  }, [destinationCities, destinationId])

  const selectedOrigin = useMemo(
    () => availableCities.find((city) => String(city.id) === String(originId)),
    [availableCities, originId],
  )

  const selectedDestination = useMemo(
    () => availableCities.find((city) => String(city.id) === String(destinationId)),
    [availableCities, destinationId],
  )

  async function search(event) {
    event?.preventDefault?.()

    if (!originId || !destinationId || !date) {
      setMessage('Informe origem, destino e data.')
      return
    }

    if (String(originId) === String(destinationId)) {
      setMessage('Origem e destino devem ser diferentes.')
      return
    }

    setLoading(true)
    setMessage('')

    try {
      const params = new URLSearchParams({
        origemId: String(originId),
        destinoId: String(destinationId),
        data: date,
        categoria: category,
        ordenar: order,
      })

      const { response, data: result } = await apiFetch(`/trips?${params.toString()}`)
      if (!response.ok) {
        setMessage(result?.mensagem || 'Não foi possível buscar viagens.')
        return
      }

      setTrips(Array.isArray(result) ? result : [])
    } catch (error) {
      console.error('Falha ao buscar viagens:', error)
      setMessage('Não foi possível conectar ao servidor de viagens.')
    } finally {
      setLoading(false)
    }
  }

  function swapRoute() {
    setOriginState(destinationState)
    setDestinationState(originState)
    setOriginId(destinationId)
    setDestinationId(originId)
  }

  function money(cents) {
    return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cents / 100)
  }

  function dateTime(value) {
    return new Intl.DateTimeFormat('pt-BR', {
      timeZone: 'America/Sao_Paulo',
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(value))
  }

  return (
    <Layout user={user} onUserChange={onUserChange}>
      <section className="search-hero">
        <div className="search-copy">
          <span className="eyebrow">ESCOLHA SEU PRÓXIMO DESTINO</span>
          <h1>Para onde vamos?</h1>
          <p>Sua viagem, seu assento.</p>
          <small className="supported-cities-note">8 cidades com viagens preparadas para os próximos 30 dias.</small>
        </div>

        <form className="search-panel" onSubmit={search}>
          <div className="location-group">
            <label>
              <span>Origem - UF</span>
              <select value={originState} onChange={(event) => setOriginState(event.target.value)}>
                {states.map((state) => <option key={state.sigla} value={state.sigla}>{state.sigla} - {state.nome}</option>)}
              </select>
            </label>
            <label>
              <span>Origem</span>
              <select value={originId} onChange={(event) => setOriginId(event.target.value)}>
                {originCities.map((city) => <option key={city.id} value={city.id}>{city.nome}</option>)}
              </select>
            </label>
          </div>

          <button className="swap-button" type="button" onClick={swapRoute} aria-label="Inverter origem e destino">⇄</button>

          <div className="location-group">
            <label>
              <span>Destino - UF</span>
              <select value={destinationState} onChange={(event) => setDestinationState(event.target.value)}>
                {states.map((state) => <option key={state.sigla} value={state.sigla}>{state.sigla} - {state.nome}</option>)}
              </select>
            </label>
            <label>
              <span>Destino</span>
              <select value={destinationId} onChange={(event) => setDestinationId(event.target.value)}>
                {destinationCities.map((city) => <option key={city.id} value={city.id}>{city.nome}</option>)}
              </select>
            </label>
          </div>

          <label className="date-field">
            <span>Data da viagem</span>
            <input type="date" value={date} onChange={(event) => setDate(event.target.value)} />
          </label>

          <button className="button-primary search-button" type="submit" disabled={loading}>
            {loading ? 'Buscando...' : 'Buscar viagens'}
          </button>
        </form>
      </section>

      <section className="results-section">
        <div className="results-header">
          <div>
            <span className="eyebrow">VIAGENS DISPONÍVEIS</span>
            <h2>{trips.length} {trips.length === 1 ? 'viagem encontrada' : 'viagens encontradas'}</h2>
            {selectedOrigin && selectedDestination && <p>{selectedOrigin.nome} → {selectedDestination.nome}</p>}
          </div>
          <div className="filters-inline">
            <label>
              <span>Categoria</span>
              <select value={category} onChange={(event) => setCategory(event.target.value)}>
                <option>Todas</option>
                <option>Executivo</option>
                <option>Semi-leito</option>
                <option>Leito</option>
                <option>Convencional</option>
              </select>
            </label>
            <label>
              <span>Ordenar</span>
              <select value={order} onChange={(event) => setOrder(event.target.value)}>
                <option value="horario">Mais cedo</option>
                <option value="preco">Menor preço</option>
              </select>
            </label>
            <button type="button" className="button-secondary" onClick={search}>Aplicar</button>
          </div>
        </div>

        <StatusMessage type="error">{message}</StatusMessage>

        <div className="trip-list">
          {trips.map((trip) => (
            <article className="trip-card" key={trip.id}>
              <div className="trip-category">{trip.onibus.categoria}</div>
              <div className="trip-main">
                <div><strong>{dateTime(trip.partida)}</strong><span>{trip.origem.nome}</span></div>
                <div className="trip-line"><span>Direto</span></div>
                <div><strong>{dateTime(trip.chegada)}</strong><span>{trip.destino.nome}</span></div>
              </div>
              <div className="trip-bus"><strong>{trip.onibus.prefixo}</strong><span>{trip.onibus.modelo}</span></div>
              <div className="trip-price"><span>Valor por passageiro</span><strong>{money(trip.precoCentavos)}</strong><small>{trip.assentosDisponiveis} assentos disponíveis</small></div>
              <Link className="button-primary" to={`/comprar/${trip.id}`}>Escolher poltronas</Link>
            </article>
          ))}
        </div>

        {!loading && trips.length === 0 && !message && (
          <div className="empty-state">Faça uma busca para consultar as viagens disponíveis.</div>
        )}
      </section>
    </Layout>
  )
}
