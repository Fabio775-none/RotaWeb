# Requisitos implementados

## Passageiro e visitante

- Busca pública por origem, destino e data.
- Estados e municípios consultados pela API do IBGE.
- Filtro por categoria e ordenação por horário/preço.
- Cadastro e login de passageiro.
- Escolha visual de poltrona.
- Confirmação de passagem com preço histórico.
- Histórico de passagens e filtros por status.
- Comprovante individual e impressão pelo navegador.
- Cancelamento antes da partida mantendo histórico.
- Perfil e alteração de senha.

## Administração

- Login administrativo em banco separado.
- Login administrativo por e-mail e senha; não requer aplicativo autenticador.
- Indicadores de operação.
- Cadastro, edição e desativação de ônibus.
- Cadastro, edição e cancelamento de viagens.
- Validação de sobreposição do veículo.
- Consulta administrativa de passagens.
- Exportação CSV.
- Auditoria de ações administrativas.

## Integridade

- Capacidade de ônibus entre 16 e 60.
- Prefixo e placa únicos.
- Origem diferente do destino.
- Chegada posterior à partida.
- Preço positivo em centavos.
- Assento dentro da capacidade.
- Índice único parcial para assento confirmado.
- Edição de viagem bloqueada quando existem passagens confirmadas.
- Cancelamento de viagem preserva e cancela passagens existentes.

## Segurança reforçada

- Separação física de negócio, autenticação de passageiros e autenticação administrativa.
- Senhas com ASP.NET Core Identity.
- Sessões opacas revogáveis; token bruto somente em cookie HttpOnly.
- Lockout e rate limiting administrativos.
- CSRF, rate limiting, lockout e headers defensivos.
- Segredos fora do repositório.
- Autorização por política e consultas com filtro de proprietário.
