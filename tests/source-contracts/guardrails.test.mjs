import { test } from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { resolve, dirname } from 'node:path'

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..')
const read = (name) => readFileSync(resolve(root, name), 'utf8')

test('instalação do PostgreSQL bloqueia reconfiguração de instalações existentes', () => {
  const setup = read('database/setup-local.ps1')
  assert.match(setup, /countSql\s*=/)
  assert.match(setup, /existingCount -gt 0/)
  assert.ok(setup.indexOf('existingCount -gt 0') < setup.indexOf('-f $tempSql'), 'pré-checagem deve ocorrer antes do SQL de criação')
})

test('compilação valida antes de substituir arquivos da interface', () => {
  const build = read('Scripts/Build-RotaWeb.ps1')
  assert.match(build, /npm ci/)
  assert.match(build, /npm test/)
  assert.match(build, /npm run build/)
  assert.match(build, /wwwroot-next-/)
  assert.match(build, /wwwroot-previous-/)
  assert.ok(build.indexOf('npm run build') < build.indexOf('Move-Item -LiteralPath $wwwroot'), 'não substituir wwwroot antes do build')
})

test('publicação compila antes de trocar o pacote anterior', () => {
  const publish = read('Scripts/Publish-RotaWeb.ps1')
  assert.match(publish, /Build-RotaWeb\.ps1/)
  assert.match(publish, /publish-next-/)
  assert.match(publish, /publish-previous-/)
})

test('os três bancos e as rotas atuais continuam declarados', () => {
  const program = read('backend/Program.cs')
  for (const bank of ['Business', 'PassengerAuth', 'AdminAuth']) assert.ok(program.includes(`"${bank}"`))
  const app = read('frontend/src/App.jsx')
  for (const route of ['/admin/login', '/cadastro', '/login']) assert.ok(app.includes(route), `rota ${route} deve permanecer`)
})
