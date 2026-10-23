CREATE TABLE matches (
    match_id uuid PRIMARY KEY,
    player_id uuid NOT NULL REFERENCES players(player_id),
    version bigint NOT NULL CHECK (version > 0),
    rules_version char(64) NOT NULL,
    status text NOT NULL CHECK (status IN ('active','ended')),
    state jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX matches_one_active_player ON matches(player_id) WHERE status='active';

CREATE TABLE match_requests (
    player_id uuid NOT NULL REFERENCES players(player_id),
    request_id uuid NOT NULL,
    request_hash char(64) NOT NULL,
    http_status integer NOT NULL,
    response jsonb NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (player_id,request_id)
);
