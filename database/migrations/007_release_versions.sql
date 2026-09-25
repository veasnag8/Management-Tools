-- Migration 007: Release Versions Table
-- Author: Software Licensing Platform Architect

CREATE TABLE IF NOT EXISTS public.release_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_id UUID NOT NULL REFERENCES public.products(id) ON DELETE CASCADE,
    version TEXT NOT NULL,
    download_url TEXT,
    release_notes TEXT,
    sha256 TEXT,
    mandatory BOOLEAN NOT NULL DEFAULT false,
    active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_product_version UNIQUE (product_id, version)
);

CREATE INDEX IF NOT EXISTS idx_releases_product ON public.release_versions(product_id);
CREATE INDEX IF NOT EXISTS idx_releases_version ON public.release_versions(version);
CREATE INDEX IF NOT EXISTS idx_releases_active ON public.release_versions(active);

ALTER TABLE public.release_versions ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    CREATE POLICY "Staff can view releases"
        ON public.release_versions FOR SELECT TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE POLICY "Admins and managers can manage releases"
        ON public.release_versions FOR ALL TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager'))
        WITH CHECK (public.get_current_user_role() IN ('admin', 'manager'));
EXCEPTION WHEN duplicate_object THEN null; END $$;
