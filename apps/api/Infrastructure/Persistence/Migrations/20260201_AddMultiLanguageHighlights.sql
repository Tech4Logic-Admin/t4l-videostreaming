-- Migration: Add multi-language support for video highlights
-- Date: 2026-02-01
-- Description: Adds columns for storing original language text and creates highlight_translations table

-- Add new columns to video_highlights table
ALTER TABLE video_highlights
ADD COLUMN IF NOT EXISTS original_text TEXT,
ADD COLUMN IF NOT EXISTS source_language VARCHAR(10);

-- Add new columns to video_summaries table
ALTER TABLE video_summaries
ADD COLUMN IF NOT EXISTS original_summary TEXT,
ADD COLUMN IF NOT EXISTS original_tldr VARCHAR(500),
ADD COLUMN IF NOT EXISTS source_language VARCHAR(10);

-- Create highlight_translations table for caching translations
CREATE TABLE IF NOT EXISTS highlight_translations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    video_highlight_id UUID NOT NULL REFERENCES video_highlights(id) ON DELETE CASCADE,
    language_code VARCHAR(10) NOT NULL,
    translated_text TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE (video_highlight_id, language_code)
);

-- Create indexes for efficient querying
CREATE INDEX IF NOT EXISTS idx_highlight_translations_video_highlight_id
ON highlight_translations(video_highlight_id);

CREATE INDEX IF NOT EXISTS idx_highlight_translations_language_code
ON highlight_translations(language_code);

-- Comments
COMMENT ON COLUMN video_highlights.original_text IS 'Original text in the source video language';
COMMENT ON COLUMN video_highlights.source_language IS 'ISO language code of the source video (e.g., hi, gu, ta)';
COMMENT ON COLUMN video_summaries.original_summary IS 'Original summary in the source video language';
COMMENT ON COLUMN video_summaries.original_tldr IS 'Original TL;DR in the source video language';
COMMENT ON COLUMN video_summaries.source_language IS 'ISO language code of the source video';
COMMENT ON TABLE highlight_translations IS 'Stores cached translations of highlights in different languages';
