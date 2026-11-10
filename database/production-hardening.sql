-- RotaWeb - endurecimento opcional após a criação inicial das tabelas em produção.
-- Execute conectado com uma conta de migração/DBA, nunca com credenciais do frontend.

-- Impede criação de novos objetos no schema public por qualquer usuário genérico.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

-- A conta específica de cada banco deve receber somente USAGE e DML após o deploy.
-- Ajuste o nome conforme o banco em que o script estiver sendo executado.
-- Exemplo para rotaweb_data:
-- GRANT USAGE ON SCHEMA public TO rotaweb_data_app;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO rotaweb_data_app;
-- GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO rotaweb_data_app;
-- ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO rotaweb_data_app;
-- ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO rotaweb_data_app;

-- Em produção, prefira uma conta separada de migração para DDL e mantenha a conta runtime sem CREATE/ALTER/DROP.
