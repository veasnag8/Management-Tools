-- Migration 001: Profiles & Admin Roles
-- Author: Software Licensing Platform Architect

DO $$ BEGIN
    CREATE TYPE user_role AS ENUM ('admin', 'manager', 'support');
EXCEPTION
    WHEN duplicate_object THEN null;
END $$;

CREATE TABLE IF NOT EXISTS public.profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    full_name TEXT NOT NULL DEFAULT '',
    role user_role NOT NULL DEFAULT 'admin',
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Index on role for fast lookups
CREATE INDEX IF NOT EXISTS idx_profiles_role ON public.profiles(role);

-- Enable RLS
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;

-- Helper function to get current user's role
CREATE OR REPLACE FUNCTION public.get_current_user_role()
RETURNS user_role AS $$
DECLARE
    current_role user_role;
BEGIN
    SELECT role INTO current_role FROM public.profiles WHERE id = auth.uid();
    RETURN current_role;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Profiles RLS Policies:
-- 1. Users can read their own profile
DO $$ BEGIN
    CREATE POLICY "Users can view own profile"
        ON public.profiles FOR SELECT
        USING (auth.uid() = id);
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- 2. Admins can view all profiles
DO $$ BEGIN
    CREATE POLICY "Admins can view all profiles"
        ON public.profiles FOR SELECT
        USING (public.get_current_user_role() = 'admin');
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- 3. Admins can update profiles
DO $$ BEGIN
    CREATE POLICY "Admins can update profiles"
        ON public.profiles FOR UPDATE
        USING (public.get_current_user_role() = 'admin')
        WITH CHECK (public.get_current_user_role() = 'admin');
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- Automatic updated_at trigger function
CREATE OR REPLACE FUNCTION public.handle_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = now();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS tr_profiles_updated_at ON public.profiles;
CREATE TRIGGER tr_profiles_updated_at
    BEFORE UPDATE ON public.profiles
    FOR EACH ROW
    EXECUTE FUNCTION public.handle_updated_at();

-- Automatically create profile on auth.users signup
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.profiles (id, full_name, role)
    VALUES (
        NEW.id,
        COALESCE(NEW.raw_user_meta_data->>'full_name', NEW.email),
        COALESCE((NEW.raw_user_meta_data->>'role')::user_role, 'admin')
    )
    ON CONFLICT (id) DO NOTHING;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();
