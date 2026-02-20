-- Script de criação inicial do banco de dados PostgreSQL
-- Cloud SQL PostgreSQL: tech-challenge-fase-5-487617:southamerica-east1:fcg
-- Database: fcg_payments

-- Conecte-se ao Cloud SQL e execute este script como usuário postgres

-- Criar o banco de dados (se necessário)
-- CREATE DATABASE fcg_payments;

-- Conectar ao banco fcg_payments
-- \c fcg_payments

-- Tabela de Pagamentos
CREATE TABLE IF NOT EXISTS "Pagamentos" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "GameId" uuid NOT NULL,
    "Amount" numeric NOT NULL,
    "Status" integer NOT NULL,
    "DataCriacao" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Pagamentos" PRIMARY KEY ("Id")
);

-- Tabela de Events (Event Sourcing)
CREATE TABLE IF NOT EXISTS "Events" (
    "EventId" uuid NOT NULL,
    "AggregateId" uuid NOT NULL,
    "EventType" character varying(100) NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "Version" integer NOT NULL,
    "CorrelationId" uuid,
    "Payload" text NOT NULL,
    "IdempotencyKey" character varying(200),
    CONSTRAINT "PK_Events" PRIMARY KEY ("EventId")
);

-- Índices para performance
CREATE INDEX IF NOT EXISTS "IX_Events_AggregateId" ON "Events" ("AggregateId");
CREATE INDEX IF NOT EXISTS "IX_Events_OccurredAt" ON "Events" ("OccurredAt");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Events_IdempotencyKey" ON "Events" ("IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;

-- Verificar tabelas criadas
SELECT tablename FROM pg_tables WHERE schemaname = 'public';
