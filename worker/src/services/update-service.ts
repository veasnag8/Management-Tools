import { SupabaseClient } from '@supabase/supabase-js';
import { Env, VersionResponseData, ServiceResult } from '../types';

export class UpdateService {
  constructor(
    private supabase: SupabaseClient,
    private env: Env
  ) {}

  /**
   * Compare two semantic version strings (e.g., '1.0.5' vs '1.0.0')
   * Returns: >0 if v1 > v2, <0 if v1 < v2, 0 if equal
   */
  private compareVersions(v1: string, v2: string): number {
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

  /**
   * Check latest version and update requirements for a product
   */
  async checkVersion(
    productCode: string,
    currentClientVersion: string = '1.0.0'
  ): Promise<ServiceResult<VersionResponseData>> {
    // 1. Fetch product
    const { data: product, error: prodErr } = await this.supabase
      .from('products')
      .select('id, product_code, name, current_version, download_url, active')
      .eq('product_code', productCode.toUpperCase())
      .single();

    if (prodErr || !product) {
      return {
        success: false,
        error: { code: 'INVALID_REQUEST', message: `Product ${productCode} not found.` },
        statusCode: 404
      };
    }

    if (!product.active) {
      return {
        success: false,
        error: { code: 'PRODUCT_DISABLED', message: 'Product is inactive.' },
        statusCode: 403
      };
    }

    // 2. Fetch all active release versions for this product ordered by created_at DESC
    const { data: releases, error: relErr } = await this.supabase
      .from('release_versions')
      .select('*')
      .eq('product_id', product.id)
      .eq('active', true)
      .order('created_at', { ascending: false });

    let latestRelease = releases && releases.length > 0 ? releases[0] : null;
    const latestVersion = latestRelease?.version || product.current_version || currentClientVersion;

    const hasUpdate = this.compareVersions(latestVersion, currentClientVersion) > 0;

    // Check if any release between current and latest is marked as mandatory
    let isMandatory = false;
    if (releases && hasUpdate) {
      for (const rel of releases) {
        if (this.compareVersions(rel.version, currentClientVersion) > 0 && rel.mandatory) {
          isMandatory = true;
          break;
        }
      }
    }

    return {
      success: true,
      data: {
        product_code: product.product_code,
        current_version: currentClientVersion,
        latest_version: latestVersion,
        download_url: latestRelease?.download_url || product.download_url || null,
        release_notes: latestRelease?.release_notes || 'Performance enhancements and bug fixes.',
        sha256: latestRelease?.sha256 || null,
        mandatory: isMandatory,
        has_update: hasUpdate
      }
    };
  }
}
