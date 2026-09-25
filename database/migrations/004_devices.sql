-- Migration 004: Devices Table
-- Author: Software Licensing Platform Architect

DO $$ BEGIN
    CREATE TYPE device_status AS ENUM ('active', 'revoked');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS public.devices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    license_id UUID NOT NULL REFERENCES public.licenses(id) ON DELETE CASCADE,
    device_id TEXT NOT NULL,
    device_name TEXT,
    os_name TEXT,
    os_version TEXT,
    app_version TEXT,
    first_activated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_ip_address INET,
    status device_status NOT NULL DEFAULT 'active',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_license_device UNIQUE (license_id, device_id)
);

CREATE INDEX IF NOT EXISTS idx_devices_license_id ON public.devices(license_id);
CREATE INDEX IF NOT EXISTS idx_devices_device_id ON public.devices(device_id);
CREATE INDEX IF NOT EXISTS idx_devices_status ON public.devices(status);
CREATE INDEX IF NOT EXISTS idx_devices_last_seen ON public.devices(last_seen_at);

ALTER TABLE public.devices ENABLE ROW LEVEL SECURITY;

DO $$ BEGIN
    CREATE POLICY "Staff can view devices"
        ON public.devices FOR SELECT TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE POLICY "Staff can manage devices"
        ON public.devices FOR ALL TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager', 'support'))
        WITH CHECK (public.get_current_user_role() IN ('admin', 'manager', 'support'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

DROP TRIGGER IF EXISTS tr_devices_updated_at ON public.devices;
CREATE TRIGGER tr_devices_updated_at
    BEFORE UPDATE ON public.devices
    FOR EACH ROW
    EXECUTE FUNCTION public.handle_updated_at();
