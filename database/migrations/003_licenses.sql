-- Migration 003: Licenses Table
-- Author: Software Licensing Platform Architect

DO $$ BEGIN
    CREATE TYPE license_status AS ENUM ('pending', 'active', 'disabled', 'expired', 'revoked');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS public.licenses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    license_key TEXT UNIQUE NOT NULL,
    customer_name TEXT,
    customer_email TEXT,
    product_id UUID NOT NULL REFERENCES public.products(id) ON DELETE RESTRICT,
    status license_status NOT NULL DEFAULT 'active',
    start_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ, -- NULL means lifetime license
    max_devices INTEGER NOT NULL DEFAULT 1 CHECK (max_devices >= 1),
    offline_grace_hours INTEGER NOT NULL DEFAULT 72 CHECK (offline_grace_hours >= 0),
    notes TEXT,
    created_by UUID REFERENCES auth.users(id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT chk_license_dates CHECK (expires_at IS NULL OR expires_at >= start_at)
);

CREATE INDEX IF NOT EXISTS idx_licenses_key ON public.licenses(license_key);
CREATE INDEX IF NOT EXISTS idx_licenses_status ON public.licenses(status);
CREATE INDEX IF NOT EXISTS idx_licenses_product ON public.licenses(product_id);
CREATE INDEX IF NOT EXISTS idx_licenses_expires_at ON public.licenses(expires_at);
CREATE INDEX IF NOT EXISTS idx_licenses_customer_email ON public.licenses(customer_email);
CREATE INDEX IF NOT EXISTS idx_licenses_customer_name ON public.licenses(customer_name);

ALTER TABLE public.licenses ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    CREATE POLICY "Staff can view licenses"
        ON public.licenses FOR SELECT TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE POLICY "Admins and managers can insert licenses"
        ON public.licenses FOR INSERT TO authenticated
        WITH CHECK (public.get_current_user_role() IN ('admin', 'manager'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE POLICY "Admins and managers can update licenses"
        ON public.licenses FOR UPDATE TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager'))
        WITH CHECK (public.get_current_user_role() IN ('admin', 'manager'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE POLICY "Only admins can delete licenses"
        ON public.licenses FOR DELETE TO authenticated
        USING (public.get_current_user_role() = 'admin');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DROP TRIGGER IF EXISTS tr_licenses_updated_at ON public.licenses;
CREATE TRIGGER tr_licenses_updated_at
    BEFORE UPDATE ON public.licenses
    FOR EACH ROW
    EXECUTE FUNCTION public.handle_updated_at();
