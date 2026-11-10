# Arquivos de configuração explicados

## backend/appsettings.json

`AllowedHosts`: hosts aceitos pelo ASP.NET Core.

`Security:AllowedOrigins`: lista de origens liberadas por CORS. Em produção, prefira frontend e API na mesma origem.

`PassengerSessionMinutes`: duração da sessão comum não persistente.

`PassengerRememberDays`: duração máxima quando “manter conectado” é solicitado.

`AdminSessionMinutes`: duração em minutos da sessão administrativa, com mínimo 5 e máximo 240.


`Database:AutoCreateDevelopment`: deve ficar `false` fora de desenvolvimento.

## appsettings.Development.json

Libera `http://localhost:5173`, criação automática de schema e dados fictícios para facilitar demonstração local.

## frontend/vite.config.js

Define porta 5173 e proxy `/api` para o backend em 5263. O proxy faz as chamadas parecerem same-origin durante o desenvolvimento.
