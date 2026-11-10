// Mapa visual de poltronas com seleção múltipla de até cinco lugares.
export default function SeatMap({ capacity, occupied = [], selected = [], maxSelected = 5, onSelect }) {
  const occupiedSet = new Set(occupied)
  const selectedSet = new Set(Array.isArray(selected) ? selected : [selected].filter(Boolean))
  const seats = Array.from({ length: capacity }, (_, index) => index + 1)

  return (
    <div className="bus-seat-wrapper">
      <div className="bus-front">
        <span>Frente do ônibus</span>
        <strong>Motorista</strong>
      </div>

      <div className="seat-map" aria-label="Mapa de poltronas">
        {seats.map((seat) => {
          const isOccupied = occupiedSet.has(seat)
          const isSelected = selectedSet.has(seat)
          const limitReached = selectedSet.size >= maxSelected && !isSelected

          return (
            <button
              key={seat}
              type="button"
              className={`seat ${isOccupied ? 'seat-occupied' : ''} ${isSelected ? 'seat-selected' : ''}`}
              disabled={isOccupied || limitReached}
              aria-pressed={isSelected}
              aria-label={`Poltrona ${seat}${isOccupied ? ' ocupada' : isSelected ? ' selecionada' : ' disponível'}`}
              onClick={() => onSelect(seat)}
              title={isOccupied ? 'Ocupada' : limitReached ? `Limite de ${maxSelected} poltronas atingido` : `Poltrona ${seat}`}
            >
              <span className="seat-icon">▣</span>
              <span>{seat}</span>
            </button>
          )
        })}
      </div>

      <div className="bus-rear">Traseira</div>
    </div>
  )
}
