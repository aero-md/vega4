-- 001_core — guild_settings + triggers (le cœur historique de VEGA).
--
-- Idempotent (CREATE ... IF NOT EXISTS) : rejouable à chaque déploiement.
--
-- Types alignés sur le schéma réellement déployé (ancien Database/createDb.sql) :
--   ulong (IDs Discord)  -> bigint
--   Guid                 -> uuid
--   DateTime             -> timestamp (SANS time zone)  ← schéma existant
--   string               -> varchar(n) / text selon la colonne d'origine
-- NB : l'app active Npgsql.EnableLegacyTimestampBehavior (Program.cs) pour que EF mappe
--      DateTime sur `timestamp` (et non `timestamptz`) et accepte ces colonnes.

CREATE EXTENSION IF NOT EXISTS pgcrypto;  -- gen_random_uuid()

CREATE TABLE IF NOT EXISTS guild_settings (
    guild_id bigint PRIMARY KEY
);

CREATE TABLE IF NOT EXISTS triggers (
    trigger_id    uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    guild_id      bigint        NOT NULL REFERENCES guild_settings (guild_id) ON DELETE CASCADE,
    pattern       varchar(255)  NOT NULL,
    response      text          NOT NULL,
    regex_options integer,
    ping_on_reply boolean       DEFAULT false,
    created_at    timestamp     NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_triggers_guild_id ON triggers (guild_id);
