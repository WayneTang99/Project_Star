CREATE TABLE pvp_snapshots (
    snapshot_id uuid PRIMARY KEY,
    player_id uuid NOT NULL REFERENCES players(player_id),
    match_id uuid NOT NULL REFERENCES matches(match_id),
    round integer NOT NULL CHECK (round > 0),
    rules_version char(64) NOT NULL,
    state text NOT NULL CHECK (octet_length(state) <= 2097152),
    created_at timestamptz NOT NULL DEFAULT now(),
    UNIQUE(match_id,round,rules_version)
);
CREATE INDEX pvp_snapshots_pool ON pvp_snapshots(rules_version,round,player_id);

CREATE TABLE pvp_battles (
    battle_id uuid PRIMARY KEY,
    player_id uuid NOT NULL REFERENCES players(player_id),
    match_id uuid NOT NULL REFERENCES matches(match_id),
    round integer NOT NULL CHECK (round > 0),
    rules_version char(64) NOT NULL,
    opponent_snapshot_id uuid REFERENCES pvp_snapshots(snapshot_id),
    is_test_opponent boolean NOT NULL,
    published_snapshot_id uuid NOT NULL UNIQUE,
    player_snapshot text NOT NULL CHECK (octet_length(player_snapshot) <= 2097152),
    input_state text NOT NULL CHECK (octet_length(input_state) <= 2097152),
    input_sha256 char(64) NOT NULL,
    expected_match_version bigint NOT NULL CHECK (expected_match_version > 1),
    status text NOT NULL DEFAULT 'pending' CHECK (status IN ('pending','running','completed','failed')),
    attempts integer NOT NULL DEFAULT 0 CHECK (attempts BETWEEN 0 AND 3),
    lease_token uuid,
    lease_until timestamptz,
    next_attempt_at timestamptz NOT NULL DEFAULT now(),
    error text,
    result jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    CHECK (is_test_opponent = (opponent_snapshot_id IS NULL)),
    CHECK ((status = 'running') = (lease_token IS NOT NULL AND lease_until IS NOT NULL)),
    CHECK ((status = 'completed') = (result IS NOT NULL AND completed_at IS NOT NULL)),
    UNIQUE(match_id,round,rules_version)
);
CREATE INDEX pvp_battles_queue ON pvp_battles(rules_version,next_attempt_at,lease_until) WHERE status IN ('pending','running');
CREATE INDEX pvp_battles_owner ON pvp_battles(player_id,created_at DESC);
