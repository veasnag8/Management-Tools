import { DeviceTokenPayload, SignedLicensePayload } from '../types';

/**
 * Mask license key for audit logging: TOOL-7F92KQ-XP48MD-9R2L -> TOOL-7F92****-9R2L
 */
export function maskLicenseKey(key: string): string {
  if (!key || key.length < 8) return '****';
  const parts = key.split('-');
  if (parts.length >= 4) {
    return `${parts[0]}-${parts[1]}****-${parts[parts.length - 1]}`;
  }
  return `${key.slice(0, 4)}****${key.slice(-4)}`;
}

// Convert string to Uint8Array buffer
function str2buf(str: string): Uint8Array {
  return new TextEncoder().encode(str);
}

// Convert buffer to Base64URL string
function buf2base64url(buf: ArrayBuffer): string {
  const bytes = new Uint8Array(buf);
  let binary = '';
  for (let i = 0; i < bytes.byteLength; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// Convert Base64URL string to Uint8Array buffer
function base64url2buf(b64url: string): Uint8Array {
  let b64 = b64url.replace(/-/g, '+').replace(/_/g, '/');
  while (b64.length % 4) {
    b64 += '=';
  }
  const binary = atob(b64);
  const bytes = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) {
    bytes[i] = binary.charCodeAt(i);
  }
  return bytes;
}

/**
 * Import HMAC key for WebCrypto
 */
async function getHmacKey(secret: string): Promise<CryptoKey> {
  const keyData = str2buf(secret || 'default-fallback-super-secret-key-32b');
  return await crypto.subtle.importKey(
    'raw',
    keyData,
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign', 'verify']
  );
}

/**
 * Generate a cryptographically signed JWT device token
 */
export async function generateDeviceToken(
  payload: { sub: string; license_id: string; product_id?: string },
  secret: string,
  expiresInSeconds = 30 * 24 * 3600 // 30 days
): Promise<string> {
  const header = { alg: 'HS256', typ: 'JWT' };
  const now = Math.floor(Date.now() / 1000);
  const fullPayload: DeviceTokenPayload = {
    ...payload,
    iat: now,
    exp: now + expiresInSeconds,
  };

  const encodedHeader = buf2base64url(str2buf(JSON.stringify(header)));
  const encodedPayload = buf2base64url(str2buf(JSON.stringify(fullPayload)));
  const unsignedToken = `${encodedHeader}.${encodedPayload}`;

  const key = await getHmacKey(secret);
  const signature = await crypto.subtle.sign('HMAC', key, str2buf(unsignedToken));
  const encodedSignature = buf2base64url(signature);

  return `${unsignedToken}.${encodedSignature}`;
}

/**
 * Verify and decode a JWT device token
 */
export async function verifyDeviceToken(
  token: string,
  secret: string
): Promise<DeviceTokenPayload | null> {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;

    const [encodedHeader, encodedPayload, encodedSignature] = parts;
    const unsignedToken = `${encodedHeader}.${encodedPayload}`;

    const key = await getHmacKey(secret);
    const signature = base64url2buf(encodedSignature);

    const isValid = await crypto.subtle.verify(
      'HMAC',
      key,
      signature,
      str2buf(unsignedToken)
    );

    if (!isValid) return null;

    const payloadJson = new TextDecoder().decode(base64url2buf(encodedPayload));
    const payload: DeviceTokenPayload = JSON.parse(payloadJson);

    // Check expiration
    const now = Math.floor(Date.now() / 1000);
    if (payload.exp && payload.exp < now) {
      return null;
    }

    return payload;
  } catch {
    return null;
  }
}

/**
 * Generate a server-signed license cache payload for offline verification.
 * Format: base64url(JSON_payload).base64url(HMAC_SHA256_signature)
 */
export async function generateSignedLicenseCache(
  payload: SignedLicensePayload,
  signingKey: string
): Promise<string> {
  const jsonStr = JSON.stringify(payload);
  const encodedPayload = buf2base64url(str2buf(jsonStr));

  const key = await getHmacKey(signingKey);
  const signature = await crypto.subtle.sign('HMAC', key, str2buf(encodedPayload));
  const encodedSignature = buf2base64url(signature);

  return `${encodedPayload}.${encodedSignature}`;
}

/**
 * Verify server-signed license cache payload
 */
export async function verifySignedLicenseCache(
  signedToken: string,
  signingKey: string
): Promise<SignedLicensePayload | null> {
  try {
    const parts = signedToken.split('.');
    if (parts.length !== 2) return null;

    const [encodedPayload, encodedSignature] = parts;
    const key = await getHmacKey(signingKey);
    const signature = base64url2buf(encodedSignature);

    const isValid = await crypto.subtle.verify(
      'HMAC',
      key,
      signature,
      str2buf(encodedPayload)
    );

    if (!isValid) return null;

    const jsonStr = new TextDecoder().decode(base64url2buf(encodedPayload));
    return JSON.parse(jsonStr) as SignedLicensePayload;
  } catch {
    return null;
  }
}
