import { readdir, readFile } from 'node:fs/promises'
import { join, relative } from 'node:path'
import { fileURLToPath } from 'node:url'
import { dirname } from 'node:path'

const root = join(dirname(fileURLToPath(import.meta.url)), '..')
const violations = []
const skipDirs = new Set(['node_modules', 'bin', 'obj', 'dist', 'publish', 'coverage', 'TestResults', '.git'])
const forbiddenFileParts = [/^\.env(?!\.example$)/, /^key-.*\.xml$/, /^secrets\.json$/, /\.pfx$/, /\.pem$/, /\.key$/, /\.backup$/, /\.dump$/, /\.pgpass$/, /\.sql\.gz$/]
const forbiddenStrings = [/RotaWeb#Admin2026!/i, /DisableAdminMfaForLocalDemo/i]

async function visit(folder) {
  for (const entry of await readdir(folder, { withFileTypes: true })) {
    const path = join(folder, entry.name)
    if (entry.isDirectory()) {
      if (entry.name === '.keys') {
        violations.push(`${relative(root,path)}: chaves não devem acompanhar o projeto`)
      } else if (!skipDirs.has(entry.name)) await visit(path)
      continue
    }
    if (!entry.isFile()) continue
    const name = relative(root, path)
    if (forbiddenFileParts.some((p) => p.test(entry.name))) {
      violations.push(`${name}: arquivo potencialmente privado`)
      continue
    }
    if (!/\.(cs|js|jsx|json|mjs|md|ps1|yml|yaml|sql)$/.test(entry.name)) continue
    const content = await readFile(path, 'utf8')
    for (const pattern of forbiddenStrings) {
      // This scanner refers to the forbidden literals in its own definitions; skip only itself.
      if (name !== 'tools/check-repository.mjs' && pattern.test(content)) {
        violations.push(`${name}: credencial fixa ou atalho administrativo inseguro encontrado`)
      }
    }
  }
}
await visit(root)
if (violations.length) {
  console.error(`A verificação encontrou ${violations.length} problema(s):\n${violations.join('\n')}`)
  process.exitCode = 1
} else {
  console.log('OK: nenhum segredo local, chave privada ou atalho administrativo conhecido encontrado.')
}
