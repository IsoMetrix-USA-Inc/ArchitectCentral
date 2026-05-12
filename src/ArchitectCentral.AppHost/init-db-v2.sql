-- ============================================================================
-- Architect Central RAG Database - V2 Enhanced Schema
-- ============================================================================
-- Description: Combined initialization script with V2 enhancements built-in
-- Version: 2.0
-- Date: 2026-05-11
-- Dependencies: PostgreSQL 13+, pgvector extension
-- ============================================================================

-- This script runs in the 'postgres' database during container initialization
-- First create the database, then connect to it and create the schema

-- Create the database if it doesn't exist
SELECT 'CREATE DATABASE "architect-central"'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'architect-central')\gexec

-- Now connect to the new database
\c architect-central

-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- ============================================================================
-- TYPES AND ENUMS
-- ============================================================================

-- Enhanced document types
CREATE TYPE doc_type_v2 AS ENUM (
    'ADR',
    'Guideline',
    'Diagram',
    'CodeExample',
    'AntiPattern',
    'BestPractice',
    'Architecture',
    'Other'
);

-- ADR status tracking
CREATE TYPE adr_status AS ENUM (
    'proposed',
    'accepted',
    'deprecated',
    'superseded',
    'unknown'
);

-- Content sentiment classification
CREATE TYPE sentiment_type AS ENUM (
    'positive',
    'negative',
    'neutral',
    'warning',
    'unknown'
);

-- ============================================================================
-- MAIN TABLES
-- ============================================================================

-- Enhanced document chunks with rich metadata
CREATE TABLE document_chunks (
    -- Core identification
    chunk_id TEXT PRIMARY KEY,
    source_doc_id TEXT NOT NULL,
    source_file_path TEXT NOT NULL,
    
    -- Document categorization
    doc_type doc_type_v2 NOT NULL,
    status adr_status DEFAULT 'unknown',
    category TEXT,
    sentiment sentiment_type DEFAULT 'neutral',
    
    -- Chunk positioning & overlap
    chunk_index INTEGER NOT NULL DEFAULT 0,
    chunk_total INTEGER NOT NULL DEFAULT 1,
    overlap_start INTEGER,
    overlap_end INTEGER,
    has_overlap BOOLEAN DEFAULT FALSE,
    
    -- Content
    section TEXT,
    heading_hierarchy TEXT[],
    content TEXT NOT NULL,
    content_tokens INTEGER,
    content_hash TEXT,
    
    -- Quality & metadata
    quality_score DECIMAL(3,2) DEFAULT 0.5 CHECK (quality_score >= 0 AND quality_score <= 1),
    has_code_examples BOOLEAN DEFAULT FALSE,
    has_diagrams BOOLEAN DEFAULT FALSE,
    completeness_score DECIMAL(3,2) CHECK (completeness_score IS NULL OR (completeness_score >= 0 AND completeness_score <= 1)),
    
    -- Tags and relationships
    technical_tags TEXT[],
    framework_refs TEXT[],
    related_adrs INTEGER[],
    superseded_by TEXT,
    
    -- Extended metadata (flexible JSONB)
    metadata JSONB,
    
    -- Timestamps
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_indexed_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    -- Vector embedding
    embedding vector(1536),
    
    -- Full-text search (generated column for hybrid search)
    search_vector tsvector GENERATED ALWAYS AS (
        setweight(to_tsvector('english', coalesce(section, '')), 'A') ||
        setweight(to_tsvector('english', content), 'B')
    ) STORED
);

-- Document source tracking
CREATE TABLE document_sources (
    source_id TEXT PRIMARY KEY,
    source_type TEXT NOT NULL,
    file_path TEXT NOT NULL UNIQUE,
    title TEXT,
    last_modified TIMESTAMP WITH TIME ZONE,
    content_hash TEXT NOT NULL,
    chunk_count INTEGER DEFAULT 0,
    processing_status TEXT DEFAULT 'pending',
    error_message TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    last_processed_at TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT chk_processing_status CHECK (
        processing_status IN ('pending', 'processing', 'completed', 'failed', 'skipped')
    )
);

-- Chunk relationships (for overlap and connections)
CREATE TABLE chunk_relationships (
    id SERIAL PRIMARY KEY,
    chunk_id TEXT NOT NULL,
    related_chunk_id TEXT NOT NULL,
    relationship_type TEXT NOT NULL,
    overlap_tokens INTEGER,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT chk_relationship_type CHECK (
        relationship_type IN ('overlaps_with', 'continues_from', 'supersedes', 'relates_to')
    ),
    CONSTRAINT unique_relationship UNIQUE (chunk_id, related_chunk_id, relationship_type)
);

-- Search query analytics
CREATE TABLE search_queries (
    id SERIAL PRIMARY KEY,
    query_text TEXT NOT NULL,
    query_hash TEXT NOT NULL,
    query_type TEXT,
    filters_applied JSONB,
    results_count INTEGER,
    avg_similarity DECIMAL(4,3),
    execution_time_ms INTEGER,
    user_agent TEXT,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT chk_query_type CHECK (
        query_type IN ('vector', 'hybrid', 'filtered', 'keyword', 'metadata')
    )
);

-- ============================================================================
-- INDEXES - Optimized for RAG Query Patterns
-- ============================================================================

-- Core lookup indexes
CREATE INDEX idx_chunks_source_doc ON document_chunks(source_doc_id);
CREATE INDEX idx_chunks_source_file ON document_chunks(source_file_path);
CREATE INDEX idx_chunks_doc_type ON document_chunks(doc_type);

-- Status and filtering
CREATE INDEX idx_chunks_status_accepted ON document_chunks(status) 
    WHERE status IN ('accepted', 'proposed');
CREATE INDEX idx_chunks_sentiment ON document_chunks(sentiment);
CREATE INDEX idx_chunks_category ON document_chunks(category) 
    WHERE category IS NOT NULL;

-- Quality filtering (partial index for high-quality chunks)
CREATE INDEX idx_chunks_quality ON document_chunks(quality_score DESC) 
    WHERE quality_score >= 0.7;

-- Composite index for common filter combinations
CREATE INDEX idx_chunks_type_status_quality 
    ON document_chunks(doc_type, status, quality_score DESC);

-- Array indexes for tag searching
CREATE INDEX idx_chunks_technical_tags ON document_chunks USING GIN(technical_tags);
CREATE INDEX idx_chunks_framework_refs ON document_chunks USING GIN(framework_refs);
CREATE INDEX idx_chunks_related_adrs ON document_chunks USING GIN(related_adrs);

-- Full-text search index (hybrid search)
CREATE INDEX idx_chunks_search_vector ON document_chunks USING GIN(search_vector);

-- JSONB metadata index
CREATE INDEX idx_chunks_metadata ON document_chunks USING GIN(metadata);

-- Vector similarity index (HNSW for fast cosine similarity)
CREATE INDEX idx_chunks_embedding ON document_chunks 
    USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

-- Change detection
CREATE INDEX idx_chunks_content_hash ON document_chunks(content_hash);

-- Temporal indexes
CREATE INDEX idx_chunks_updated_at ON document_chunks(updated_at DESC);
CREATE INDEX idx_chunks_last_indexed ON document_chunks(last_indexed_at DESC);

-- document_sources indexes
CREATE INDEX idx_source_type ON document_sources(source_type);
CREATE INDEX idx_source_status ON document_sources(processing_status);
CREATE INDEX idx_source_hash ON document_sources(content_hash);
CREATE INDEX idx_source_last_modified ON document_sources(last_modified DESC);

-- chunk_relationships indexes
CREATE INDEX idx_chunk_rel_chunk ON chunk_relationships(chunk_id);
CREATE INDEX idx_chunk_rel_related ON chunk_relationships(related_chunk_id);
CREATE INDEX idx_chunk_rel_type ON chunk_relationships(relationship_type);

-- search_queries indexes
CREATE INDEX idx_search_queries_hash ON search_queries(query_hash);
CREATE INDEX idx_search_queries_created ON search_queries(created_at DESC);
CREATE INDEX idx_search_queries_type ON search_queries(query_type);

-- ============================================================================
-- VIEWS - Convenient Query Shortcuts
-- ============================================================================

-- High-quality accepted content
CREATE VIEW vw_quality_accepted_chunks AS
SELECT 
    chunk_id,
    source_doc_id,
    source_file_path,
    doc_type,
    status,
    category,
    section,
    content,
    quality_score,
    technical_tags,
    framework_refs,
    embedding,
    created_at,
    updated_at
FROM document_chunks
WHERE status = 'accepted' 
  AND quality_score >= 0.7
  AND doc_type IN ('ADR', 'Guideline', 'BestPractice');

-- Anti-patterns and warnings
CREATE VIEW vw_antipatterns AS
SELECT 
    chunk_id,
    source_doc_id,
    source_file_path,
    doc_type,
    section,
    content,
    sentiment,
    technical_tags,
    quality_score,
    created_at
FROM document_chunks
WHERE sentiment IN ('negative', 'warning')
   OR doc_type = 'AntiPattern';

-- Recent content updates
CREATE VIEW vw_recent_updates AS
SELECT 
    chunk_id,
    source_doc_id,
    source_file_path,
    doc_type,
    section,
    content,
    quality_score,
    updated_at,
    last_indexed_at
FROM document_chunks
WHERE updated_at >= CURRENT_TIMESTAMP - INTERVAL '30 days'
ORDER BY updated_at DESC;

-- ============================================================================
-- FUNCTIONS AND TRIGGERS
-- ============================================================================

-- Update timestamp trigger
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trigger_update_chunks_timestamp
    BEFORE UPDATE ON document_chunks
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Calculate quality score function (called during enrichment)
CREATE OR REPLACE FUNCTION calculate_quality_score(
    p_doc_type doc_type_v2,
    p_content_length INTEGER,
    p_has_code BOOLEAN,
    p_has_diagrams BOOLEAN,
    p_completeness DECIMAL DEFAULT 0.5
) RETURNS DECIMAL AS $$
DECLARE
    v_score DECIMAL := 0.5;
BEGIN
    -- Base score from content length (normalized to 0.3-0.7 range)
    v_score := LEAST(0.7, 0.3 + (p_content_length::DECIMAL / 5000) * 0.4);
    
    -- Boost for code examples (+0.15)
    IF p_has_code THEN
        v_score := v_score + 0.15;
    END IF;
    
    -- Boost for diagrams (+0.1)
    IF p_has_diagrams THEN
        v_score := v_score + 0.1;
    END IF;
    
    -- Weight by completeness
    v_score := v_score * (0.7 + (p_completeness * 0.3));
    
    -- Cap at 1.0
    RETURN LEAST(1.0, v_score);
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- Extract ADR status from content
CREATE OR REPLACE FUNCTION extract_adr_status(p_content TEXT)
RETURNS adr_status AS $$
BEGIN
    IF p_content ~* 'status:?\s*(accepted|approve)' THEN
        RETURN 'accepted'::adr_status;
    ELSIF p_content ~* 'status:?\s*(proposed|draft|pending)' THEN
        RETURN 'proposed'::adr_status;
    ELSIF p_content ~* 'status:?\s*(deprecated|obsolete)' THEN
        RETURN 'deprecated'::adr_status;
    ELSIF p_content ~* 'status:?\s*superseded' THEN
        RETURN 'superseded'::adr_status;
    ELSE
        RETURN 'unknown'::adr_status;
    END IF;
END;
$$ LANGUAGE plpgsql IMMUTABLE;

-- ============================================================================
-- LEGACY COMPATIBILITY (Optional)
-- ============================================================================

-- Alias for old doc_type enum (if needed for backward compatibility)
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'doc_type') THEN
        CREATE TYPE doc_type AS ENUM ('ADR', 'Guideline', 'Diagram', 'Other');
    END IF;
END $$;

-- ============================================================================
-- VERIFICATION
-- ============================================================================

DO $$ 
BEGIN
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Database Initialization Complete';
    RAISE NOTICE '========================================';
    RAISE NOTICE 'Tables created: 4 (document_chunks, document_sources, chunk_relationships, search_queries)';
    RAISE NOTICE 'Indexes created: 27';
    RAISE NOTICE 'Views created: 3';
    RAISE NOTICE 'Functions created: 3';
    RAISE NOTICE 'Triggers created: 1';
    RAISE NOTICE 'Ready for vectorization!';
END $$;
