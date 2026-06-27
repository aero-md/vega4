-- 003_polls — sondages avec boutons de vote.
--
--   polls       : un sondage (question, options localisées, fenêtre de vote)
--   poll_votes  : un vote par utilisateur et par sondage (PK composite (poll_id, user_id))
--
-- options : string[] -> text[]. Types alignés sur le schéma déployé (cf. 001_core.sql).

CREATE TABLE IF NOT EXISTS polls (
    poll_id      uuid          PRIMARY KEY DEFAULT gen_random_uuid(),
    guild_id     bigint        NOT NULL,
    channel_id   bigint        NOT NULL,
    message_id   bigint        NOT NULL DEFAULT 0,
    creator_id   bigint        NOT NULL,
    question     varchar(2000) NOT NULL,
    image_url    varchar(500),
    options      text[]        NOT NULL DEFAULT '{}',
    end_at       timestamp     NOT NULL,
    created_at   timestamp     NOT NULL,
    is_completed boolean       NOT NULL DEFAULT false,
    locale       varchar(10)   NOT NULL DEFAULT 'en-US'
);

CREATE INDEX IF NOT EXISTS idx_polls_guild_id ON polls (guild_id);

-- PollService.Initialize balaye les sondages encore ouverts après un redémarrage.
CREATE INDEX IF NOT EXISTS idx_polls_pending_end_at
    ON polls (end_at)
    WHERE is_completed = false;

CREATE TABLE IF NOT EXISTS poll_votes (
    poll_id      uuid      NOT NULL REFERENCES polls (poll_id) ON DELETE CASCADE,
    user_id      bigint    NOT NULL,
    option_index integer   NOT NULL,
    voted_at     timestamp NOT NULL,
    PRIMARY KEY (poll_id, user_id)
);
