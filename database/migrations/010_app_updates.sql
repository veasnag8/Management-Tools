-- Migration 010: App Updates Table for Tool Dubber Auto Update Center
-- Author: Software Licensing & Update Platform Architect

CREATE TABLE IF NOT EXISTS public.app_updates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    product_code TEXT NOT NULL DEFAULT 'SNA_DUBBER',
    version TEXT NOT NULL,
    download_url TEXT NOT NULL,
    release_notes TEXT,
    checksum_sha256 TEXT,
    is_mandatory BOOLEAN NOT NULL DEFAULT false,
    status TEXT NOT NULL DEFAULT 'published', -- 'published', 'draft', 'archived'
    min_supported_version TEXT DEFAULT '1.0.0',
    published_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_app_updates_version UNIQUE (product_code, version)
);

CREATE INDEX IF NOT EXISTS idx_app_updates_product ON public.app_updates(product_code);
CREATE INDEX IF NOT EXISTS idx_app_updates_version ON public.app_updates(version);
CREATE INDEX IF NOT EXISTS idx_app_updates_status ON public.app_updates(status);
CREATE INDEX IF NOT EXISTS idx_app_updates_published_at ON public.app_updates(published_at DESC);

ALTER TABLE public.app_updates ENABLE ROW LEVEL SECURITY;

-- Allow public / client reading of published updates (no service-role key required)
DO $$ BEGIN
    CREATE POLICY "Public read published updates"
        ON public.app_updates FOR SELECT
        USING (status = 'published');
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- Allow authorized admins and managers to manage app updates
DO $$ BEGIN
    CREATE POLICY "Admins and managers can manage app updates"
        ON public.app_updates FOR ALL TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager'))
        WITH CHECK (public.get_current_user_role() IN ('admin', 'manager'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- Sync trigger between app_updates and release_versions
CREATE OR REPLACE FUNCTION public.sync_app_update_to_release_version()
RETURNS TRIGGER AS $$
DECLARE
    prod_id UUID;
BEGIN
    SELECT id INTO prod_id FROM public.products WHERE product_code = NEW.product_code LIMIT 1;
    IF prod_id IS NOT NULL THEN
        INSERT INTO public.release_versions (product_id, version, download_url, release_notes, sha256, mandatory, active, created_at)
        VALUES (prod_id, NEW.version, NEW.download_url, NEW.release_notes, NEW.checksum_sha256, NEW.is_mandatory, (NEW.status = 'published'), NEW.published_at)
        ON CONFLICT (product_id, version) DO UPDATE SET
            download_url = EXCLUDED.download_url,
            release_notes = EXCLUDED.release_notes,
            sha256 = EXCLUDED.sha256,
            mandatory = EXCLUDED.mandatory,
            active = EXCLUDED.active;
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

DROP TRIGGER IF EXISTS trg_sync_app_update ON public.app_updates;
CREATE TRIGGER trg_sync_app_update
    AFTER INSERT OR UPDATE ON public.app_updates
    FOR EACH ROW
    EXECUTE FUNCTION public.sync_app_update_to_release_version();

-- Seed initial 1.2.0 release into app_updates if not exists
INSERT INTO public.app_updates (product_code, version, download_url, release_notes, checksum_sha256, is_mandatory, status, published_at)
VALUES (
    'SNA_DUBBER',
    '1.2.0',
    'https://github.com/veasnag8/Management-Tools/releases/download/v1.2.0/SNA-Dubber-Setup-v1.2.0.exe',
    'Initial release of Tool Dubber v1.2.0 with AI Video Dubbing, Khmer Speech Synthesis, and Auto Update Center.',
    NULL,
    false,
    'published',
    now()
) ON CONFLICT (product_code, version) DO NOTHING;
