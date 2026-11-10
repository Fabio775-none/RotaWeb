import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { test } from 'node:test';
const base = resolve(import.meta.dirname, '../..');
const source = (p) => readFileSync(resolve(base,p), 'utf8');
test('login administrativo emite sessão completa após senha', () => {
  const controller = source('backend/Controllers/AdminAuthController.cs');
  assert.match(controller, /IssueAuthenticatedAsync\(HttpContext, user\)/);
  assert.doesNotMatch(controller, /IssuePreAuthAsync|VerifyTwoFactorTokenAsync|requiresMfa|mfa\/setup|mfa\/enable/);
});
test('validador não exige habilitar 2FA', () => {
  const handler=source('backend/Security/AdminSessionAuthenticationHandler.cs');
  assert.match(handler, /session\.Estagio != "Autenticada"/);
  assert.doesNotMatch(handler, /TwoFactorEnabled/);
});
test('interface não oferece ativação do autenticador', () => {
  assert.equal(existsSync(resolve(base, 'frontend/src/pages/AdminMfa.jsx')), false);
  const app=source('frontend/src/App.jsx');
  assert.doesNotMatch(app, /import AdminMfa/);
  assert.match(app, /path="\/admin\/mfa" element={<Navigate to="\/admin\/login" replace \/>/);
});
test('seed jamais apaga viagens ou passagens', () => {
  const seed=source('backend/Services/DatabaseBootstrapper.cs');
  assert.doesNotMatch(seed, /RemoveRange\(tickets\)|RemoveRange\(trips\)/);
  assert.match(seed, /db\.Passagens\.AnyAsync\(\)/);
  assert.match(seed, /db\.Viagens\.AnyAsync\(\)/);
});
