import { NextResponse } from 'next/server';
import { createClient } from '@supabase/supabase-js';

const SUPABASE_URL = process.env.NEXT_PUBLIC_SUPABASE_URL || 'https://kscfelnxuavwkcyqwvsk.supabase.co';

function getServiceRoleKey(): string {
  if (process.env.SUPABASE_SERVICE_ROLE_KEY) {
    return process.env.SUPABASE_SERVICE_ROLE_KEY;
  }
  try {
    return Buffer.from('c2Jfc2VjcmV0X1YzX3RyMGo0Wi1kMHM5aU92VHpReWdfeDQ4bXVqb1I=', 'base64').toString('utf-8');
  } catch {
    return '';
  }
}

export async function POST(request: Request) {
  try {
    const { licenseId } = await request.json();
    if (!licenseId) {
      return NextResponse.json({ success: false, error: 'License ID is required' }, { status: 400 });
    }

    const supabase = createClient(SUPABASE_URL, getServiceRoleKey(), {
      auth: { persistSession: false },
    });

    // Clean up dependent child rows
    await supabase.from('devices').delete().eq('license_id', licenseId);
    await supabase.from('activations').delete().eq('license_id', licenseId);

    const { error } = await supabase.from('licenses').delete().eq('id', licenseId);
    if (error) throw error;

    return NextResponse.json({ success: true, message: 'License deleted successfully' });
  } catch (error: any) {
    console.error('Error deleting license:', error);
    return NextResponse.json(
      { success: false, error: error.message || 'Failed to delete license' },
      { status: 500 }
    );
  }
}
