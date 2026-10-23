CREATE TABLE players (
    player_id uuid PRIMARY KEY,
    enabled boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now()
);

-- 密码只是登录方式；存档与对局将引用 players.player_id。
CREATE TABLE password_credentials (
    player_id uuid PRIMARY KEY REFERENCES players(player_id),
    username varchar(32) NOT NULL UNIQUE,
    password_hash text NOT NULL
);

CREATE TABLE invitations (
    code_hash char(64) PRIMARY KEY,
    max_uses integer NOT NULL CHECK (max_uses BETWEEN 1 AND 1000),
    uses integer NOT NULL DEFAULT 0 CHECK (uses >= 0 AND uses <= max_uses),
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE auth_sessions (
    session_id uuid PRIMARY KEY,
    player_id uuid NOT NULL REFERENCES players(player_id),
    access_hash char(64) NOT NULL UNIQUE,
    refresh_hash char(64) NOT NULL UNIQUE,
    access_expires_at timestamptz NOT NULL,
    refresh_expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX auth_sessions_player ON auth_sessions(player_id);
CREATE INDEX auth_sessions_expiry ON auth_sessions(refresh_expires_at);
