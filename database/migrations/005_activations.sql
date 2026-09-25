-- Migration 005: Activations Audit Trail Table
-- Author: Software Licensing Platform Architect

DO $$ BEGIN
    CREATE TYPE activation_action AS ENUM (
        'activate',
        'validate',
        'heartbeat',
        'deactivate',
        'reset',
        'blocked',
        'expired'
    );
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS public.activations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    license_id UUID REFERENCES public.licenses(id) ON DELETE CASCADE,
    device_id TEXT,
    action activation_action NOT NULL,
    ip_address INET,
    user_agent TEXT,
    metadata JSONB DEFAULT '{}'::jsonb,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_activations_license_id ON public.activations(license_id);
CREATE INDEX IF NOT EXISTS idx_activations_device_id ON public.activations(device_id);
CREATE INDEX IF NOT EXISTS idx_activations_action ON public.activations(action);
CREATE INDEX IF NOT EXISTS idx_activations_created_at ON public.activations(created_at DESC);

ALTER TABLE public.activations ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    CREATE POLICY "Staff can view activations"
        ON public.activations FOR SELECT TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;
