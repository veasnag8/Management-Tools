-- Migration 009: Clear Activity and Audit Logs Policies and RPC
-- Author: Software Licensing Platform Architect

-- 1. Grant DELETE policy on activations for admins and managers
DO $$ BEGIN
    CREATE POLICY "Admins and managers can delete activations"
        ON public.activations FOR DELETE TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- 2. Grant DELETE policy on license_logs for admins and managers
DO $$ BEGIN
    CREATE POLICY "Admins and managers can delete license logs"
        ON public.license_logs FOR DELETE TO authenticated
        USING (public.get_current_user_role() IN ('admin', 'manager'));
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- 3. Stored Procedure for atomic log purging
CREATE OR REPLACE FUNCTION public.clear_activity_logs(p_type TEXT DEFAULT 'all')
RETURNS jsonb
LANGUAGE plpgsql
SECURITY DEFINER
AS $$
BEGIN
    IF public.get_current_user_role() NOT IN ('admin', 'manager') THEN
        RETURN jsonb_build_object('success', false, 'message', 'Unauthorized. Admin or manager role required.');
    END IF;

    IF p_type = 'client' THEN
        DELETE FROM public.activations;
    ELSIF p_type = 'admin' THEN
        DELETE FROM public.license_logs;
    ELSE
        DELETE FROM public.activations;
        DELETE FROM public.license_logs;
    END IF;

    RETURN jsonb_build_object('success', true, 'message', 'Activity logs cleared successfully.');
END;
$$;

