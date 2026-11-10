import { useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { apiFetch, apiJson } from '../api/client.js'
import Layout from '../components/Layout.jsx'
import SeatMap from '../components/SeatMap.jsx'
import StatusMessage from '../components/StatusMessage.jsx'

const MAX_SEATS = 5
const DOCUMENT_TYPES = ['RG/CIN', 'Passaporte', 'Certidão de nascimento']
const GUARDIAN_RELATIONS = ['Pai/Mãe', 'Responsável legal', 'Avô/Avó', 'Irmão/Irmã', 'Tio/Tia', 'Terceiro autorizado']

function ageOn(dateString, referenceDateString) {
  if (!dateString || !referenceDateString) return null
  const [by, bm, bd] = dateString.split('-').map(Number)
  const [ry, rm, rd] = referenceDateString.split('-').map(Number)
  if (![by, bm, bd, ry, rm, rd].every(Number.isFinite)) return null
  let age = ry - by
  if (rm < bm || (rm === bm && rd < bd)) age--
  return age
}

function emptyPassenger(defaultName = '', defaultBirthDate = '') {
  return {
    passageiro: defaultName,
    documento: '',
    tipoDocumento: 'RG/CIN',
    dataNascimento: defaultBirthDate,
    responsavelNome: '',
    responsavelDocumento: '',
    responsavelDataNascimento: '',
    responsavelVinculo: 'Pai/Mãe',
    responsavelAcompanha: false,
    documentacaoMenorConfirmada: false,
  }
}

// Fluxo protegido de seleção múltipla de poltronas, regras de menor, Pix demonstrativo e emissão.
export default function Compra({ user, onUserChange }) {
  const { id } = useParams()
  const navigate = useNavigate()

  const [trip, setTrip] = useState(null)
  const [occupied, setOccupied] = useState([])
  const [selectedSeats, setSelectedSeats] = useState([])
  const [passengerData, setPassengerData] = useState({})
  const [pixCode, setPixCode] = useState('')
  const [pixTxId, setPixTxId] = useState('')
  const [copied, setCopied] = useState(false)
  const [loading, setLoading] = useState(true)
  const [message, setMessage] = useState('')

  useEffect(() => {
    let active = true

    async function load() {
      try {
        const [tripResult, seatResult] = await Promise.all([
          apiFetch(`/trips/${id}`),
          apiFetch(`/trips/${id}/seats`),
        ])

        if (!tripResult.response.ok) {
          if (active) setMessage(tripResult.data?.mensagem || 'Viagem não encontrada.')
          return
        }

        if (active) {
          setTrip(tripResult.data)
          setOccupied(seatResult.response.ok ? seatResult.data.ocupados : [])
        }
      } catch {
        if (active) setMessage('Não foi possível carregar a viagem.')
      } finally {
        if (active) setLoading(false)
      }
    }

    load()
    return () => { active = false }
  }, [id])

  const totalCentavos = useMemo(
    () => (trip?.precoCentavos || 0) * selectedSeats.length,
    [trip, selectedSeats.length],
  )

  const travelDate = trip?.partida?.slice(0, 10) || ''
  const money = (cents) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(cents / 100)

  function passengerAge(seat) {
    return ageOn(passengerData[seat]?.dataNascimento, travelDate)
  }

  function toggleSeat(seat) {
    setMessage('')
    setCopied(false)
    setPixCode('')
    setPixTxId('')

    setSelectedSeats((current) => {
      if (current.includes(seat)) {
        setPassengerData((data) => {
          const next = { ...data }
          delete next[seat]
          return next
        })
        return current.filter((item) => item !== seat)
      }

      if (current.length >= MAX_SEATS) {
        setMessage(`Você pode comprar no máximo ${MAX_SEATS} poltronas por vez.`)
        return current
      }

      setPassengerData((data) => ({
        ...data,
        [seat]: emptyPassenger(current.length === 0 ? (user?.nome || '') : '', current.length === 0 ? (user?.dataNascimento || '') : ''),
      }))
      return [...current, seat].sort((a, b) => a - b)
    })
  }

  function updatePassenger(seat, field, value) {
    setPixCode('')
    setPixTxId('')
    setCopied(false)
    setPassengerData((current) => ({
      ...current,
      [seat]: {
        ...current[seat],
        [field]: value,
      },
    }))
  }

  function validateSelection() {
    if (selectedSeats.length < 1 || selectedSeats.length > MAX_SEATS) {
      setMessage(`Escolha de 1 a ${MAX_SEATS} poltronas.`)
      return false
    }

    for (const seat of selectedSeats) {
      const passenger = passengerData[seat]
      if (!passenger || passenger.passageiro.trim().length < 3 || passenger.documento.trim().length < 3 || !passenger.dataNascimento) {
        setMessage(`Preencha nome, data de nascimento e documento do passageiro da poltrona ${seat}.`)
        return false
      }

      const age = ageOn(passenger.dataNascimento, travelDate)
      if (age === null || age < 0) {
        setMessage(`A data de nascimento do passageiro da poltrona ${seat} é inválida.`)
        return false
      }

      if (!DOCUMENT_TYPES.includes(passenger.tipoDocumento)) {
        setMessage(`Escolha o tipo de documento da poltrona ${seat}.`)
        return false
      }

      if (age >= 12 && passenger.tipoDocumento === 'Certidão de nascimento') {
        setMessage(`Na poltrona ${seat}, passageiro com 12 anos ou mais deve usar documento oficial com foto.`)
        return false
      }

      if (age < 18) {
        const guardianAge = ageOn(passenger.responsavelDataNascimento, travelDate)
        if (
          passenger.responsavelNome.trim().length < 3 ||
          passenger.responsavelDocumento.trim().length < 3 ||
          guardianAge === null || guardianAge < 18 ||
          !passenger.responsavelVinculo ||
          !passenger.responsavelAcompanha ||
          !passenger.documentacaoMenorConfirmada
        ) {
          setMessage(`A poltrona ${seat} é de menor de 18 anos. Informe um responsável adulto acompanhante e confirme a documentação.`)
          return false
        }
      }
    }

    return true
  }

  function generatePixDemo() {
    setMessage('')
    if (!validateSelection()) return

    const randomPart = Math.random().toString(36).slice(2, 8).toUpperCase()
    const txid = `RWDEMO-${Date.now().toString(36).toUpperCase()}-${randomPart}`
    const seats = selectedSeats.join(',')
    const value = (totalCentavos / 100).toFixed(2)

    const code = `ROTAWEB-PIX-DEMO|TXID=${txid}|VIAGEM=${id}|POLTRONAS=${seats}|VALOR=${value}|SEM-TRANSACAO-BANCARIA`
    setPixTxId(txid)
    setPixCode(code)
    setCopied(false)
  }

  async function copyPixCode() {
    if (!pixCode) return
    try {
      await navigator.clipboard.writeText(pixCode)
      setCopied(true)
    } catch {
      setMessage('Não foi possível copiar automaticamente. Selecione o código e copie manualmente.')
    }
  }

  async function confirmDemoPayment() {
    if (!pixCode || !pixTxId) {
      setMessage('Gere o código Pix de demonstração antes de confirmar.')
      return
    }
    if (!validateSelection()) return

    setLoading(true)
    setMessage('')

    try {
      const passageiros = selectedSeats.map((seat) => ({
        assento: seat,
        passageiro: passengerData[seat].passageiro.trim(),
        documento: passengerData[seat].documento.trim(),
        tipoDocumento: passengerData[seat].tipoDocumento,
        dataNascimento: passengerData[seat].dataNascimento,
        responsavelNome: passengerData[seat].responsavelNome.trim() || null,
        responsavelDocumento: passengerData[seat].responsavelDocumento.trim() || null,
        responsavelDataNascimento: passengerData[seat].responsavelDataNascimento || null,
        responsavelVinculo: passengerData[seat].responsavelVinculo || null,
        responsavelAcompanha: passengerData[seat].responsavelAcompanha,
        documentacaoMenorConfirmada: passengerData[seat].documentacaoMenorConfirmada,
      }))

      const { response, data } = await apiJson('/tickets/batch', 'POST', {
        viagemId: Number(id),
        passageiros,
        precoEsperadoCentavos: trip.precoCentavos,
        referenciaPagamentoDemo: pixTxId,
      })

      if (!response.ok) {
        setMessage(data?.mensagem || 'Não foi possível emitir as passagens.')
        const seatResult = await apiFetch(`/trips/${id}/seats`)
        if (seatResult.response.ok) setOccupied(seatResult.data.ocupados)
        return
      }

      const ids = data.tickets.map((ticket) => ticket.id).join(',')
      const params = new URLSearchParams({ ids, txid: pixTxId })
      navigate(`/compra/concluida?${params.toString()}`)
    } catch {
      setMessage('Não foi possível conectar ao servidor.')
    } finally {
      setLoading(false)
    }
  }

  if (loading && !trip) {
    return <Layout user={user} onUserChange={onUserChange}><div className="page-state">Carregando viagem...</div></Layout>
  }

  if (!trip) {
    return <Layout user={user} onUserChange={onUserChange}><div className="content-width"><StatusMessage type="error">{message}</StatusMessage></div></Layout>
  }

  return (
    <Layout user={user} onUserChange={onUserChange}>
      <section className="content-width purchase-grid purchase-grid-wide">
        <div className="panel">
          <span className="eyebrow">MAPA DE POLTRONAS</span>
          <h1>Escolha até 5 lugares</h1>
          <p className="muted">Verde: selecionada. Cinza: ocupada. As poltronas só são reservadas quando a emissão é concluída no servidor.</p>

          <div className="seat-selection-counter">
            <strong>{selectedSeats.length}/{MAX_SEATS}</strong>
            <span>poltronas selecionadas</span>
          </div>

          <SeatMap
            capacity={trip.assentos}
            occupied={occupied}
            selected={selectedSeats}
            maxSelected={MAX_SEATS}
            onSelect={toggleSeat}
          />

          <div className="seat-legend">
            <span><i className="legend-dot available" />Disponível</span>
            <span><i className="legend-dot selected" />Selecionada</span>
            <span><i className="legend-dot occupied" />Ocupada</span>
          </div>

          <div className="legal-info-box">
            <strong>Regra de menores no RotaWeb</strong>
            <p>O comprador deve ter 18 anos ou mais. Passageiros menores são permitidos com responsável adulto acompanhante e documentação para conferência no embarque.</p>
          </div>
        </div>

        <div className="panel purchase-summary-panel">
          <span className="eyebrow">PASSAGEIROS E PAGAMENTO</span>
          <h2>{trip.origem.nome} → {trip.destino.nome}</h2>
          <p>{new Date(trip.partida).toLocaleString('pt-BR', { timeZone: 'America/Sao_Paulo' })}</p>
          <p><strong>{trip.onibus.prefixo}</strong> · {trip.onibus.modelo} · {trip.onibus.categoria}</p>
          <p className="muted">{money(trip.precoCentavos)} por passageiro</p>
          <p className="price-highlight">Total: {money(totalCentavos)}</p>

          {selectedSeats.length === 0 && (
            <div className="empty-state compact">Escolha pelo menos uma poltrona no mapa.</div>
          )}

          <div className="passenger-seat-list">
            {selectedSeats.map((seat) => {
              const age = passengerAge(seat)
              const isMinor = age !== null && age >= 0 && age < 18
              return (
                <div className="passenger-seat-card passenger-seat-card-expanded" key={seat}>
                  <div className="passenger-seat-title">
                    <strong>Poltrona {seat}</strong>
                    <span>{age === null ? 'Passageiro' : `${age} anos${isMinor ? ' · menor' : ''}`}</span>
                  </div>

                  <label>
                    <span>Nome</span>
                    <input
                      value={passengerData[seat]?.passageiro || ''}
                      onChange={(event) => updatePassenger(seat, 'passageiro', event.target.value)}
                      minLength={3}
                      maxLength={100}
                      placeholder="Nome do passageiro"
                    />
                  </label>

                  <label>
                    <span>Data de nascimento</span>
                    <input
                      type="date"
                      value={passengerData[seat]?.dataNascimento || ''}
                      onChange={(event) => updatePassenger(seat, 'dataNascimento', event.target.value)}
                    />
                  </label>

                  <label>
                    <span>Tipo de documento</span>
                    <select value={passengerData[seat]?.tipoDocumento || 'RG/CIN'} onChange={(event) => updatePassenger(seat, 'tipoDocumento', event.target.value)}>
                      {DOCUMENT_TYPES.map((type) => <option key={type} value={type}>{type}</option>)}
                    </select>
                  </label>

                  <label>
                    <span>Número do documento</span>
                    <input
                      value={passengerData[seat]?.documento || ''}
                      onChange={(event) => updatePassenger(seat, 'documento', event.target.value)}
                      minLength={3}
                      maxLength={40}
                      placeholder="Documento para conferência"
                    />
                  </label>

                  {isMinor && (
                    <div className="minor-guardian-box">
                      <div className="minor-guardian-heading">
                        <strong>Responsável pelo menor</strong>
                        <span>{age < 16 ? 'Menor de 16: atenção especial à documentação' : 'Política RotaWeb para 16 e 17 anos'}</span>
                      </div>

                      <div className="minor-guardian-grid">
                        <label>
                          <span>Nome do responsável</span>
                          <input value={passengerData[seat]?.responsavelNome || ''} onChange={(event) => updatePassenger(seat, 'responsavelNome', event.target.value)} placeholder="Nome completo" />
                        </label>
                        <label>
                          <span>Documento do responsável</span>
                          <input value={passengerData[seat]?.responsavelDocumento || ''} onChange={(event) => updatePassenger(seat, 'responsavelDocumento', event.target.value)} placeholder="RG/CIN ou equivalente" />
                        </label>
                        <label>
                          <span>Nascimento do responsável</span>
                          <input type="date" value={passengerData[seat]?.responsavelDataNascimento || ''} onChange={(event) => updatePassenger(seat, 'responsavelDataNascimento', event.target.value)} />
                        </label>
                        <label>
                          <span>Vínculo</span>
                          <select value={passengerData[seat]?.responsavelVinculo || 'Pai/Mãe'} onChange={(event) => updatePassenger(seat, 'responsavelVinculo', event.target.value)}>
                            {GUARDIAN_RELATIONS.map((relation) => <option key={relation} value={relation}>{relation}</option>)}
                          </select>
                        </label>
                      </div>

                      <label className="checkbox-line">
                        <input type="checkbox" checked={Boolean(passengerData[seat]?.responsavelAcompanha)} onChange={(event) => updatePassenger(seat, 'responsavelAcompanha', event.target.checked)} />
                        <span>O responsável adulto informado acompanhará o menor durante a viagem.</span>
                      </label>

                      <label className="checkbox-line">
                        <input type="checkbox" checked={Boolean(passengerData[seat]?.documentacaoMenorConfirmada)} onChange={(event) => updatePassenger(seat, 'documentacaoMenorConfirmada', event.target.checked)} />
                        <span>Confirmo que os documentos exigidos do menor e do responsável serão apresentados no embarque.</span>
                      </label>
                    </div>
                  )}
                </div>
              )
            })}
          </div>

          <StatusMessage type="error">{message}</StatusMessage>

          {!pixCode && (
            <button className="button-primary button-block" type="button" onClick={generatePixDemo} disabled={selectedSeats.length === 0 || loading}>
              Gerar Pix copia e cola de demonstração
            </button>
          )}

          {pixCode && (
            <div className="pix-demo-box">
              <div className="pix-demo-heading">
                <div><span className="eyebrow">PIX DEMONSTRATIVO</span><strong>{money(totalCentavos)}</strong></div>
                <span className="status-pill status-confirmada">Aguardando simulação</span>
              </div>
              <p className="demo-warning">Código acadêmico. Não é um Pix real e não movimenta dinheiro.</p>
              <label>
                <span>Pix copia e cola</span>
                <textarea value={pixCode} readOnly rows={4} />
              </label>
              <div className="action-row">
                <button className="button-secondary" type="button" onClick={copyPixCode}>{copied ? 'Copiado!' : 'Copiar código'}</button>
                <button className="button-primary" type="button" onClick={confirmDemoPayment} disabled={loading}>
                  {loading ? 'Emitindo...' : 'Simular pagamento e emitir passagens'}
                </button>
              </div>
            </div>
          )}
        </div>
      </section>
    </Layout>
  )
}
