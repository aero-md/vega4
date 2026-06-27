-- 004_reminders — rappels programmés.
--
-- Table présente dans le schéma déployé (ancien Database/createDb.sql). Le code C#
-- correspondant (Reminder entity, ReminderService) est actuellement COMMENTÉ : la table
-- existe mais n'est pas encore exploitée par le bot. Conservée ici pour rester fidèle au
-- schéma réel et préparer la réactivation de la feature.
-- Types alignés sur le schéma déployé (cf. 001_core.sql).

CREATE TABLE IF NOT EXISTS reminders (
    reminder_id  uuid      PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id      bigint    NOT NULL,
    guild_id     bigint    NOT NULL REFERENCES guild_settings (guild_id) ON DELETE CASCADE,
    channel_id   bigint    NOT NULL,
    message      text      NOT NULL,
    remind_at    timestamp NOT NULL,
    created_at   timestamp NOT NULL DEFAULT now(),
    is_completed boolean   NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS idx_reminders_user_guild ON reminders (user_id, guild_id);
CREATE INDEX IF NOT EXISTS idx_reminders_guild_id   ON reminders (guild_id);

-- Balayage des rappels à envoyer (non complétés, par échéance).
CREATE INDEX IF NOT EXISTS idx_reminders_remind_at
    ON reminders (remind_at)
    WHERE is_completed = false;
