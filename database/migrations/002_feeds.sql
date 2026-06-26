-- 002_feeds — système de feeds Reddit -> Discord.
--
--   feeds               : un feed = un subreddit posté dans un channel (FeedProperties)
--   feeds_recent_posts  : historique des post IDs déjà publiés (anti-doublon)
--   feed_configuration  : configuration globale single-row (id = 1, CHECK + seed)
--
-- status : enum FeedStatus en int (0=Active, 1=ChannelDeleted, 2=TopicUnavailable, 3=Suspended).
-- Types alignés sur le schéma déployé (cf. 001_core.sql).

CREATE TABLE IF NOT EXISTS feeds (
    feed_id             uuid         PRIMARY KEY DEFAULT gen_random_uuid(),
    guild_id            bigint       NOT NULL REFERENCES guild_settings (guild_id) ON DELETE CASCADE,
    channel_id          bigint       NOT NULL,
    topic               varchar(255) NOT NULL,
    interval_in_minutes integer      NOT NULL,
    start_at_minute     integer      NOT NULL,
    allow_nsfw          boolean      NOT NULL DEFAULT false,
    status              integer      NOT NULL DEFAULT 0,
    created_at          timestamp    NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_feeds_guild_id   ON feeds (guild_id);
CREATE INDEX IF NOT EXISTS idx_feeds_channel_id ON feeds (channel_id);

CREATE TABLE IF NOT EXISTS feeds_recent_posts (
    feed_id   uuid        NOT NULL REFERENCES feeds (feed_id) ON DELETE CASCADE,
    post_id   varchar(64) NOT NULL,
    posted_at timestamp   NOT NULL,
    PRIMARY KEY (feed_id, post_id)
);

CREATE INDEX IF NOT EXISTS idx_frp_feed_id ON feeds_recent_posts (feed_id);
CREATE INDEX IF NOT EXISTS idx_frp_post_id ON feeds_recent_posts (post_id);

CREATE TABLE IF NOT EXISTS feed_configuration (
    id                     integer     NOT NULL DEFAULT 1,
    fetch_size             integer     NOT NULL DEFAULT 70,
    fetch_interval_minutes integer     NOT NULL DEFAULT 60,
    history_size           integer     NOT NULL DEFAULT 60,
    sort_mode              varchar(20) NOT NULL DEFAULT 'hot',
    max_feeds_per_guild    integer     NOT NULL DEFAULT 5,
    CONSTRAINT feed_configuration_pkey PRIMARY KEY (id),
    CONSTRAINT feed_configuration_single_row CHECK (id = 1)
);

-- Ligne de configuration par défaut (rejouable sans doublon).
INSERT INTO feed_configuration (id) VALUES (1) ON CONFLICT (id) DO NOTHING;
