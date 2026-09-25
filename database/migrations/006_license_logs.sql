-- Migration 006: License Admin Audit Logs Table
-- Author: Software Licensing Platform Architect

CREATE TABLE IF NOT EXISTS public.license_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    license_id UUID REFERENCES public.licenses(id) ON DELETE SET NULL,
    admin_id UUID REFERENCES auth.users(id) ON DELETE SET NULL,
    action TEXT NOT NULL,
    old_value JSONB,
    new_value JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_license_logs_license_id ON public.license_logs(license_id);
CREATE INDEX IF NOT EXISTS idx_license_logs_admin_id ON public.license_logs(admin_id);
CREATE INDEX IF NOT EXISTS idx_license_logs_action ON public.license_logs(action);
CREATE INDEX IF NOT EXISTS idx_license_logs_created_at ON public.license_logs(created_at DESC);

ALTER TABLE public.license_logs ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    CREATE POLICY "Staff can view license logs"
        ON public.license_logs FOR SELECT TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE POLICY "Staff can create license logs"
        ON public.license_logs FOR INSERT TO authenticated
        WITH CHECK (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;
