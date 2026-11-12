import { Env, VersionResponseData } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { UpdateService } from '../services/update-service';
import { jsonResponse, errorResponse } from '../middleware/validation';

function compareVersions(v1: string, v2: string): number {
  const parts1 = v1.replace(/^v/i, '').split('.').map(p => parseInt(p, 10) || 0);
  const parts2 = v2.replace(/^v/i, '').split('.').map(p => parseInt(p, 10) || 0);
  const maxLen = Math.max(parts1.length, parts2.length);
  for (let i = 0; i < maxLen; i++) {
    const p1 = parts1[i] || 0;
    const p2 = parts2[i] || 0;
    if (p1 > p2) return 1;
    if (p1 < p2) return -1;
  }
  return 0;
}

export async function handleVersion(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  const product = url.searchParams.get('product');
  const currentVersion = url.searchParams.get('current_version') || '1.0.0';

  if (!product) {
    return errorResponse('INVALID_REQUEST', 'Query parameter "product" is required.', 400);
  }

  try {
    const supabase = getSupabaseClient(env);
    const updateService = new UpdateService(supabase, env);
    const result = await updateService.checkVersion(product, currentVersion);
    if (result.success && result.data) {
      return jsonResponse(result.data, { status: 200 });
    }
  } catch {
    // Fallback to GitHub raw version.json when Supabase env vars are missing or unreachable
  }

  try {
    const ghUrl = `https://raw.githubusercontent.com/veasnag8/Management-Tools/main/version.json?t=${Date.now()}`;
    const ghResp = await fetch(ghUrl, {
      headers: { 'User-Agent': 'ToolDubber-Update-Worker/1.0' }
    });
    if (ghResp.ok) {
      const ghData = await ghResp.json() as any;
      const latestVersion = ghData.latest_version || currentVersion;
      const hasUpdate = compareVersions(latestVersion, currentVersion) > 0;
      const responseData: VersionResponseData = {
        product_code: ghData.product || product.toUpperCase(),
        current_version: currentVersion,
        latest_version: latestVersion,
        download_url: ghData.download_url || null,
        release_notes: ghData.release_notes || 'Performance enhancements and bug fixes.',
        sha256: ghData.sha256 || null,
        mandatory: Boolean(ghData.mandatory),
        has_update: hasUpdate
      };
      return jsonResponse(responseData, { status: 200 });
    }
  } catch {
    // Ignore and return error below
  }

  return errorResponse('INTERNAL_ERROR', 'Unable to retrieve version information.', 500);
}
