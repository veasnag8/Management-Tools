-- Migration 008: Atomic Stored Procedures & Functions
-- Author: Software Licensing Platform Architect

-- 1. Atomic Device Activation Procedure
CREATE OR REPLACE FUNCTION public.activate_device_atomic(
    p_license_key TEXT,
    p_device_id TEXT,
    p_device_name TEXT,
    p_os_name TEXT,
    p_os_version TEXT,
    p_app_version TEXT,
    p_ip_address INET,
    p_user_agent TEXT
)
RETURNS JSONB AS $$
DECLARE
    v_license RECORD;
    v_product RECORD;
    v_device RECORD;
    v_active_device_count INT;
BEGIN
    -- 1. Fetch and lock license record
    SELECT * INTO v_license
    FROM public.licenses
    WHERE license_key = p_license_key
    FOR UPDATE;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'code', 'INVALID_LICENSE', 'message', 'License key does not exist.');
    END IF;

    -- 2. Verify Product status
    SELECT * INTO v_product
    FROM public.products
    WHERE id = v_license.product_id;

    IF NOT FOUND OR v_product.active = false THEN
        RETURN jsonb_build_object('success', false, 'code', 'PRODUCT_DISABLED', 'message', 'Product is disabled or inactive.');
    END IF;

    -- 3. Verify License status
    IF v_license.status = 'disabled' THEN
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_DISABLED', 'message', 'License has been disabled.');
    ELSIF v_license.status = 'revoked' THEN
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_REVOKED', 'message', 'License has been revoked.');
    ELSIF v_license.status = 'expired' THEN
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_EXPIRED', 'message', 'License has expired.');
    ELSIF v_license.status != 'active' THEN
        RETURN jsonb_build_object('success', false, 'code', 'INVALID_LICENSE_STATUS', 'message', 'License is not active.');
    END IF;

    -- 4. Verify Start Date
    IF v_license.start_at > now() THEN
        RETURN jsonb_build_object('success', false, 'code', 'NOT_STARTED', 'message', 'License validity period has not started yet.');
    END IF;

    -- 5. Verify Expiry Date
    IF v_license.expires_at IS NOT NULL AND v_license.expires_at <= now() THEN
        UPDATE public.licenses SET status = 'expired' WHERE id = v_license.id;
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_EXPIRED', 'message', 'License has expired.');
    END IF;

    -- 6. Check existing device registration
    SELECT * INTO v_device
    FROM public.devices
    WHERE license_id = v_license.id AND device_id = p_device_id
    FOR UPDATE;

    IF FOUND THEN
        IF v_device.status = 'revoked' THEN
            RETURN jsonb_build_object('success', false, 'code', 'DEVICE_REVOKED', 'message', 'This device has been revoked. Contact administrator.');
        END IF;

        UPDATE public.devices
        SET device_name = COALESCE(p_device_name, device_name),
            os_name = COALESCE(p_os_name, os_name),
            os_version = COALESCE(p_os_version, os_version),
            app_version = COALESCE(p_app_version, app_version),
            last_seen_at = now(),
            last_ip_address = p_ip_address
        WHERE id = v_device.id;
    ELSE
        -- 7. Count current active devices
        SELECT COUNT(*) INTO v_active_device_count
        FROM public.devices
        WHERE license_id = v_license.id AND status = 'active';

        IF v_active_device_count >= v_license.max_devices THEN
            INSERT INTO public.activations (license_id, device_id, action, ip_address, user_agent, metadata)
            VALUES (v_license.id, p_device_id, 'blocked', p_ip_address, p_user_agent, jsonb_build_object('reason', 'DEVICE_LIMIT_REACHED', 'max_devices', v_license.max_devices));

            RETURN jsonb_build_object('success', false, 'code', 'DEVICE_LIMIT_REACHED', 'message', 'Maximum device limit reached for this license.');
        END IF;

        INSERT INTO public.devices (
            license_id, device_id, device_name, os_name, os_version, app_version, first_activated_at, last_seen_at, last_ip_address, status
        )
        VALUES (
            v_license.id, p_device_id, p_device_name, p_os_name, p_os_version, p_app_version, now(), now(), p_ip_address, 'active'
        ) RETURNING * INTO v_device;
    END IF;

    -- 8. Record successful activation log
    INSERT INTO public.activations (license_id, device_id, action, ip_address, user_agent, metadata)
    VALUES (v_license.id, p_device_id, 'activate', p_ip_address, p_user_agent, jsonb_build_object(
        'device_name', p_device_name,
        'app_version', p_app_version,
        'os', p_os_name || ' ' || p_os_version
    ));

    RETURN jsonb_build_object(
        'success', true,
        'license_id', v_license.id,
        'license_key', v_license.license_key,
        'product_id', v_product.id,
        'product_code', v_product.product_code,
        'product_name', v_product.name,
        'device_id', p_device_id,
        'status', 'active',
        'expires_at', v_license.expires_at,
        'offline_grace_hours', v_license.offline_grace_hours,
        'server_time', now()
    );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;


-- 2. Atomic Device Validation Function
CREATE OR REPLACE FUNCTION public.validate_device_atomic(
    p_license_id UUID,
    p_device_id TEXT,
    p_ip_address INET,
    p_user_agent TEXT
)
RETURNS JSONB AS $$
DECLARE
    v_license RECORD;
    v_product RECORD;
    v_device RECORD;
BEGIN
    SELECT * INTO v_license
    FROM public.licenses
    WHERE id = p_license_id;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'code', 'INVALID_LICENSE', 'message', 'License not found.');
    END IF;

    SELECT * INTO v_product
    FROM public.products
    WHERE id = v_license.product_id;

    IF NOT FOUND OR v_product.active = false THEN
        RETURN jsonb_build_object('success', false, 'code', 'PRODUCT_DISABLED', 'message', 'Product is inactive.');
    END IF;

    IF v_license.status = 'disabled' THEN
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_DISABLED', 'message', 'License has been disabled.');
    ELSIF v_license.status = 'revoked' THEN
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_REVOKED', 'message', 'License has been revoked.');
    ELSIF v_license.status = 'expired' THEN
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_EXPIRED', 'message', 'License has expired.');
    END IF;

    IF v_license.expires_at IS NOT NULL AND v_license.expires_at <= now() THEN
        UPDATE public.licenses SET status = 'expired' WHERE id = v_license.id;
        RETURN jsonb_build_object('success', false, 'code', 'LICENSE_EXPIRED', 'message', 'License has expired.');
    END IF;

    SELECT * INTO v_device
    FROM public.devices
    WHERE license_id = v_license.id AND device_id = p_device_id;

    IF NOT FOUND THEN
        RETURN jsonb_build_object('success', false, 'code', 'INVALID_DEVICE', 'message', 'Device is not registered with this license.');
    END IF;

    IF v_device.status = 'revoked' THEN
        RETURN jsonb_build_object('success', false, 'code', 'DEVICE_REVOKED', 'message', 'Device authorization has been revoked.');
    END IF;

    UPDATE public.devices
    SET last_seen_at = now(),
        last_ip_address = COALESCE(p_ip_address, last_ip_address)
    WHERE id = v_device.id;

    RETURN jsonb_build_object(
        'success', true,
        'valid', true,
        'license_id', v_license.id,
        'device_id', p_device_id,
        'product_code', v_product.product_code,
        'status', 'active',
        'expires_at', v_license.expires_at,
        'offline_grace_hours', v_license.offline_grace_hours,
        'server_time', now()
    );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;
