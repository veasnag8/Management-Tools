-- Migration 011: Token Usage Telemetry Table
-- Records actual Gemini token consumption from SNA-Dubber instances in real-time

CREATE TABLE IF NOT EXISTS public.token_usages (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    license_id UUID NOT NULL REFERENCES public.licenses(id) ON DELETE CASCADE,
    device_id TEXT NOT NULL,
    model_name TEXT NOT NULL DEFAULT 'gemini-2.5-flash',
    prompt_tokens INTEGER NOT NULL DEFAULT 0,
    completion_tokens INTEGER NOT NULL DEFAULT 0,
    total_tokens INTEGER NOT NULL DEFAULT 0,
    task_type TEXT NOT NULL DEFAULT 'translate', -- 'translate' | 'transcribe' | 'metadata'
    task_description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_token_usages_license_id ON public.token_usages(license_id);
CREATE INDEX IF NOT EXISTS idx_token_usages_created_at ON public.token_usages(created_at DESC);
CREATE INDEX IF NOT EXISTS idx_token_usages_device_id ON public.token_usages(device_id);

ALTER TABLE public.token_usages ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    CREATE POLICY "Staff can view token usages"
        ON public.token_usages FOR SELECT TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE POLICY "Service and staff can insert token usages"
        ON public.token_usages FOR INSERT TO authenticated
        WITH CHECK (true);
EXCEPTION WHEN duplicate_object THEN null; END $$;
