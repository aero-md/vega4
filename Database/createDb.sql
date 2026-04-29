-- ============================================
-- 1) CREATE DATABASE
-- ============================================
CREATE DATABASE my_database
    WITH OWNER = vega_user
    ENCODING = 'UTF8'
    LC_COLLATE = 'en_US.utf8'
    LC_CTYPE = 'en_US.utf8'
    TEMPLATE = template0;

\connect my_database;

-- ============================================
-- 2) EXTENSIONS
-- ============================================
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- ============================================
-- 3) SCHEMA
-- ============================================
CREATE SCHEMA IF NOT EXISTS public AUTHORIZATION vega_user;

SET search_path TO public;

-- ============================================
-- 4) TABLES
-- ============================================

-- Table: guild_settings
CREATE TABLE public.guild_settings (
    guild_id bigint NOT NULL
);

-- Table: feeds
CREATE TABLE public.feeds (
    feed_id uuid DEFAULT gen_random_uuid() NOT NULL,
    guild_id bigint NOT NULL,
    channel_id bigint NOT NULL,
    topic varchar(255) NOT NULL,
    interval_in_minutes integer NOT NULL,
    start_at_minute integer NOT NULL,
    allow_nsfw boolean NOT NULL DEFAULT false,
    status integer NOT NULL DEFAULT 0,
    created_at timestamp NOT NULL
);

-- Table: triggers
CREATE TABLE public.triggers (
    trigger_id uuid DEFAULT gen_random_uuid() NOT NULL,
    pattern varchar(255) NOT NULL,
    response text NOT NULL,
    regex_options integer,
    ping_on_reply boolean DEFAULT false,
    guild_id bigint NOT NULL,
    created_at timestamp DEFAULT now() NOT NULL
);

-- Table: feeds_recent_posts
CREATE TABLE public.feeds_recent_posts (
    feed_id uuid NOT NULL,
    post_id varchar(64) NOT NULL,
    posted_at timestamp NOT NULL
);

-- Table: reminders
CREATE TABLE public.reminders (
    reminder_id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id bigint NOT NULL,
    guild_id bigint NOT NULL,
    channel_id bigint NOT NULL,
    message text NOT NULL,
    remind_at timestamp NOT NULL,
    created_at timestamp DEFAULT now() NOT NULL,
    is_completed boolean DEFAULT false NOT NULL
);

-- Table: feed_configuration (single-row global config)
CREATE TABLE public.feed_configuration (
    id integer NOT NULL DEFAULT 1,
    fetch_size integer NOT NULL DEFAULT 70,
    fetch_interval_minutes integer NOT NULL DEFAULT 60,
    history_size integer NOT NULL DEFAULT 60,
    sort_mode varchar(20) NOT NULL DEFAULT 'hot',
    max_feeds_per_guild integer NOT NULL DEFAULT 5,
    CONSTRAINT feed_configuration_pkey PRIMARY KEY (id),
    CONSTRAINT feed_configuration_single_row CHECK (id = 1)
);

-- Seed default configuration row
INSERT INTO public.feed_configuration (id) VALUES (1);

-- ============================================
-- 5) PRIMARY KEYS
-- ============================================

ALTER TABLE public.guild_settings
    ADD CONSTRAINT guild_settings_pkey PRIMARY KEY (guild_id);

ALTER TABLE public.feeds
    ADD CONSTRAINT feeds_pkey PRIMARY KEY (feed_id);

ALTER TABLE public.triggers
    ADD CONSTRAINT triggers_pkey PRIMARY KEY (trigger_id);

ALTER TABLE public.feeds_recent_posts
    ADD CONSTRAINT feeds_recent_posts_pkey PRIMARY KEY (feed_id, post_id);

ALTER TABLE public.reminders
    ADD CONSTRAINT reminders_pkey PRIMARY KEY (reminder_id);

-- ============================================
-- 6) FOREIGN KEYS
-- ============================================

-- feeds.guild_id FK guild_settings.guild_id
ALTER TABLE public.feeds
    ADD CONSTRAINT feeds_guild_fk
    FOREIGN KEY (guild_id)
    REFERENCES public.guild_settings (guild_id)
    ON DELETE CASCADE;

-- triggers.guild_id FK guild_settings.guild_id
ALTER TABLE public.triggers
    ADD CONSTRAINT triggers_guild_fk
    FOREIGN KEY (guild_id)
    REFERENCES public.guild_settings (guild_id)
    ON DELETE CASCADE;

-- feeds_recent_posts.feed_id FK feeds.feed_id
ALTER TABLE public.feeds_recent_posts
    ADD CONSTRAINT feeds_recent_posts_feed_fk
    FOREIGN KEY (feed_id)
    REFERENCES public.feeds (feed_id)
    ON DELETE CASCADE;

-- reminders.guild_id FK guild_settings.guild_id
ALTER TABLE public.reminders
    ADD CONSTRAINT reminders_guild_fk
    FOREIGN KEY (guild_id)
    REFERENCES public.guild_settings (guild_id)
    ON DELETE CASCADE;

-- ============================================
-- 7) INDEXES (optimisation)
-- ============================================

-- Pour les recherches par guild
CREATE INDEX idx_feeds_guild_id ON public.feeds (guild_id);
CREATE INDEX idx_triggers_guild_id ON public.triggers (guild_id);

-- Pour les recherches par channel
CREATE INDEX idx_feeds_channel_id ON public.feeds (channel_id);

-- Pour les recherches par feed dans feeds_recent_posts
CREATE INDEX idx_frp_feed_id ON public.feeds_recent_posts (feed_id);

-- Pour les recherches par post_id
CREATE INDEX idx_frp_post_id ON public.feeds_recent_posts (post_id);

-- Pour les recherches de reminders par utilisateur et guild
CREATE INDEX idx_reminders_user_guild ON public.reminders (user_id, guild_id);

-- Pour les recherches de reminders non complétés à envoyer
CREATE INDEX idx_reminders_remind_at ON public.reminders (remind_at) WHERE is_completed = false;

-- Pour les recherches par guild
CREATE INDEX idx_reminders_guild_id ON public.reminders (guild_id);

-- ============================================
-- END
-- ============================================
