-- Tech4Logic Video Search - Database Initialization Script
-- This script creates the necessary extensions and seed data

-- Enable necessary extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Create initial tables (will be managed by EF Core migrations in production)
-- These are created here for quick local development setup

-- Video Assets Table
CREATE TABLE IF NOT EXISTS video_assets (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID,
    title VARCHAR(500) NOT NULL,
    description VARCHAR(5000),
    tags TEXT[],
    language_hint VARCHAR(10),
    duration_ms BIGINT,
    status VARCHAR(50) NOT NULL DEFAULT 'Uploading',
    blob_path VARCHAR(1000) NOT NULL,
    thumbnail_path VARCHAR(1000),
    master_playlist_path VARCHAR(1000),
    created_by_oid VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    allowed_group_ids TEXT[],
    allowed_user_oids TEXT[]
);

CREATE INDEX IF NOT EXISTS idx_video_assets_status ON video_assets(status);
CREATE INDEX IF NOT EXISTS idx_video_assets_created_by ON video_assets(created_by_oid);
CREATE INDEX IF NOT EXISTS idx_video_assets_tenant ON video_assets(tenant_id);

-- Video Processing Jobs Table
CREATE TABLE IF NOT EXISTS video_processing_jobs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    video_id UUID NOT NULL REFERENCES video_assets(id) ON DELETE CASCADE,
    stage VARCHAR(50) NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    attempts INT DEFAULT 0,
    progress INT DEFAULT 0,
    progress_message VARCHAR(500),
    last_error VARCHAR(5000),
    external_job_id VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    started_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_jobs_video_stage ON video_processing_jobs(video_id, stage);
CREATE INDEX IF NOT EXISTS idx_jobs_status ON video_processing_jobs(status);

-- Moderation Results Table
CREATE TABLE IF NOT EXISTS moderation_results (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    video_id UUID NOT NULL UNIQUE REFERENCES video_assets(id) ON DELETE CASCADE,
    malware_scan_status VARCHAR(50) DEFAULT 'Pending',
    content_safety_status VARCHAR(50) DEFAULT 'Pending',
    reasons TEXT[],
    highest_severity VARCHAR(50),
    reviewer_decision VARCHAR(50),
    reviewer_oid VARCHAR(100),
    reviewer_notes VARCHAR(2000),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reviewed_at TIMESTAMPTZ
);

-- Transcript Segments Table
CREATE TABLE IF NOT EXISTS transcript_segments (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    video_id UUID NOT NULL REFERENCES video_assets(id) ON DELETE CASCADE,
    start_ms BIGINT NOT NULL,
    end_ms BIGINT NOT NULL,
    text TEXT NOT NULL,
    detected_language VARCHAR(10),
    speaker VARCHAR(200),
    embedding_vector FLOAT[],
    confidence FLOAT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_segments_video ON transcript_segments(video_id);
CREATE INDEX IF NOT EXISTS idx_segments_video_start ON transcript_segments(video_id, start_ms);

-- Video Variants Table (HLS encoded variants)
CREATE TABLE IF NOT EXISTS video_variants (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    video_id UUID NOT NULL REFERENCES video_assets(id) ON DELETE CASCADE,
    quality VARCHAR(50) NOT NULL,
    width INT NOT NULL,
    height INT NOT NULL,
    video_bitrate_kbps INT NOT NULL,
    audio_bitrate_kbps INT NOT NULL,
    playlist_path VARCHAR(1000),
    segments_path VARCHAR(1000),
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    progress INT DEFAULT 0,
    progress_message VARCHAR(500),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_variants_video ON video_variants(video_id);

-- Video Highlights Table (AI-extracted key points)
CREATE TABLE IF NOT EXISTS video_highlights (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    video_id UUID NOT NULL REFERENCES video_assets(id) ON DELETE CASCADE,
    text TEXT NOT NULL,
    original_text TEXT,
    source_language VARCHAR(10),
    category VARCHAR(50) NOT NULL,
    importance FLOAT,
    timestamp_ms BIGINT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_highlights_video ON video_highlights(video_id);
CREATE INDEX IF NOT EXISTS idx_highlights_category ON video_highlights(category);

-- Highlight Translations Table (cached translations)
CREATE TABLE IF NOT EXISTS highlight_translations (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    video_highlight_id UUID NOT NULL REFERENCES video_highlights(id) ON DELETE CASCADE,
    language_code VARCHAR(10) NOT NULL,
    translated_text TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (video_highlight_id, language_code)
);

CREATE INDEX IF NOT EXISTS idx_highlight_translations_highlight ON highlight_translations(video_highlight_id);

-- Video Summaries Table (AI-generated summaries)
CREATE TABLE IF NOT EXISTS video_summaries (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    video_id UUID NOT NULL UNIQUE REFERENCES video_assets(id) ON DELETE CASCADE,
    summary TEXT,
    tldr VARCHAR(500),
    original_summary TEXT,
    original_tldr VARCHAR(500),
    source_language VARCHAR(10),
    keywords TEXT[],
    topics TEXT[],
    sentiment VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_summaries_video ON video_summaries(video_id);

-- User Profiles Table
CREATE TABLE IF NOT EXISTS user_profiles (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID,
    oid VARCHAR(100) NOT NULL UNIQUE,
    email VARCHAR(500) NOT NULL,
    display_name VARCHAR(500) NOT NULL,
    group_ids TEXT[],
    role VARCHAR(50) NOT NULL DEFAULT 'Viewer',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    last_login_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_users_oid ON user_profiles(oid);
CREATE INDEX IF NOT EXISTS idx_users_email ON user_profiles(email);

-- Audit Logs Table
CREATE TABLE IF NOT EXISTS audit_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID,
    actor_oid VARCHAR(100) NOT NULL,
    action VARCHAR(100) NOT NULL,
    target_type VARCHAR(100) NOT NULL,
    target_id UUID,
    ip_address VARCHAR(50),
    user_agent VARCHAR(500),
    metadata JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_audit_actor ON audit_logs(actor_oid);
CREATE INDEX IF NOT EXISTS idx_audit_action ON audit_logs(action);
CREATE INDEX IF NOT EXISTS idx_audit_created ON audit_logs(created_at);

-- Search Query Logs Table
CREATE TABLE IF NOT EXISTS search_query_logs (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID,
    actor_oid VARCHAR(100) NOT NULL,
    query VARCHAR(1000) NOT NULL,
    language VARCHAR(10),
    results_count INT,
    latency_ms BIGINT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_search_logs_created ON search_query_logs(created_at);

-- Daily Metrics Table
CREATE TABLE IF NOT EXISTS daily_metrics (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID,
    date DATE NOT NULL,
    uploads INT DEFAULT 0,
    approved INT DEFAULT 0,
    rejected INT DEFAULT 0,
    quarantined INT DEFAULT 0,
    avg_index_time_ms FLOAT DEFAULT 0,
    searches INT DEFAULT 0,
    errors INT DEFAULT 0,
    total_videos_duration_ms BIGINT DEFAULT 0,
    unique_users INT DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE(tenant_id, date)
);

CREATE INDEX IF NOT EXISTS idx_metrics_date ON daily_metrics(date);

-- Insert seed data for development
INSERT INTO user_profiles (oid, email, display_name, role, group_ids)
VALUES
    ('dev-admin-oid', 'admin@tech4logic.com', 'Dev Admin', 'Admin', ARRAY['admins', 'all-users']),
    ('dev-uploader-oid', 'uploader@tech4logic.com', 'Dev Uploader', 'Uploader', ARRAY['uploaders', 'all-users']),
    ('dev-reviewer-oid', 'reviewer@tech4logic.com', 'Dev Reviewer', 'Reviewer', ARRAY['reviewers', 'all-users']),
    ('dev-viewer-oid', 'viewer@tech4logic.com', 'Dev Viewer', 'Viewer', ARRAY['viewers', 'all-users'])
ON CONFLICT (oid) DO NOTHING;

-- Insert sample video for development
INSERT INTO video_assets (id, title, description, tags, status, blob_path, created_by_oid, allowed_group_ids, duration_ms)
VALUES
    ('550e8400-e29b-41d4-a716-446655440001', 'Welcome to Tech4Logic', 'Introduction video about Tech4Logic platform', ARRAY['intro', 'welcome'], 'Published', 'approved/sample-1.mp4', 'dev-admin-oid', ARRAY['all-users'], 120000),
    ('550e8400-e29b-41d4-a716-446655440002', 'Product Demo', 'Demo of our video search capabilities', ARRAY['demo', 'product'], 'Published', 'approved/sample-2.mp4', 'dev-uploader-oid', ARRAY['all-users'], 180000),
    ('550e8400-e29b-41d4-a716-446655440003', 'Tutorial: Upload Videos', 'How to upload and manage videos', ARRAY['tutorial', 'howto'], 'Queued', 'quarantine/sample-3.mp4', 'dev-uploader-oid', ARRAY['uploaders'], 90000)
ON CONFLICT DO NOTHING;

-- Upload Sessions Table
CREATE TABLE IF NOT EXISTS upload_sessions (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id UUID,
    file_name VARCHAR(500) NOT NULL,
    content_type VARCHAR(100) NOT NULL,
    file_size BIGINT NOT NULL,
    chunk_size BIGINT NOT NULL,
    total_chunks INT NOT NULL,
    uploaded_chunks INT DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'Created',
    blob_path VARCHAR(1000) NOT NULL,
    created_by_oid VARCHAR(100) NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ NOT NULL,
    title VARCHAR(500) NOT NULL,
    description VARCHAR(5000),
    tags TEXT[],
    language_hint VARCHAR(10),
    block_ids TEXT[],
    video_asset_id UUID REFERENCES video_assets(id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_upload_sessions_created_by ON upload_sessions(created_by_oid);
CREATE INDEX IF NOT EXISTS idx_upload_sessions_status ON upload_sessions(status);
CREATE INDEX IF NOT EXISTS idx_upload_sessions_expires ON upload_sessions(expires_at);

-- Insert sample transcript segments for the published videos
INSERT INTO transcript_segments (video_id, start_ms, end_ms, text, detected_language, speaker, confidence)
VALUES
    ('550e8400-e29b-41d4-a716-446655440001', 0, 8000, 'Welcome to Tech4Logic Video Search demonstration.', 'en', 'Speaker 1', 0.95),
    ('550e8400-e29b-41d4-a716-446655440001', 8500, 18000, 'This platform allows you to search through video content with ease.', 'en', 'Speaker 1', 0.92),
    ('550e8400-e29b-41d4-a716-446655440001', 18500, 28000, 'Our advanced AI transcribes videos in multiple languages.', 'en', 'Speaker 1', 0.94),
    ('550e8400-e29b-41d4-a716-446655440001', 28500, 38000, 'You can jump to specific moments in any video using timeline search.', 'en', 'Speaker 1', 0.91),
    ('550e8400-e29b-41d4-a716-446655440001', 38500, 48000, 'The system supports role-based access control for enterprise security.', 'en', 'Speaker 1', 0.93),
    ('550e8400-e29b-41d4-a716-446655440002', 0, 10000, 'Let me show you how our video search feature works.', 'en', 'Speaker 1', 0.96),
    ('550e8400-e29b-41d4-a716-446655440002', 10500, 22000, 'Simply type your query in any language and press search.', 'en', 'Speaker 1', 0.94),
    ('550e8400-e29b-41d4-a716-446655440002', 22500, 35000, 'Results show matching videos with timeline markers you can click.', 'en', 'Speaker 1', 0.92)
ON CONFLICT DO NOTHING;

-- Database initialization completed successfully!
