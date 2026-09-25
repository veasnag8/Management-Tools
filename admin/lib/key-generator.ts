const CHARSET = '23456789ABCDEFGHJKMNPQRSTUVWXYZ';

function getRandomChar(): string {
  const array = new Uint32Array(1);
  if (typeof window !== 'undefined' && window.crypto) {
    window.crypto.getRandomValues(array);
  } else {
    const crypto = require('crypto');
    const buf = crypto.randomBytes(4);
    array[0] = buf.readUInt32LE(0);
  }
  return CHARSET[array[0] % CHARSET.length];
}

function getRandomSegment(length: number): string {
  let result = '';
  for (let i = 0; i < length; i++) {
    result += getRandomChar();
  }
  return result;
}

export function generateLicenseKey(prefix: string = 'TOOL'): string {
  const p = prefix.trim().toUpperCase().replace(/[^A-Z0-9]/g, '') || 'TOOL';
  const seg1 = getRandomSegment(6);
  const seg2 = getRandomSegment(6);
  const seg3 = getRandomSegment(4);
  return `${p}-${seg1}-${seg2}-${seg3}`;
}

export function generateBulkLicenseKeys(count: number, prefix: string = 'TOOL'): string[] {
  const keys = new Set<string>();
  while (keys.size < count) {
    keys.add(generateLicenseKey(prefix));
  }
  return Array.from(keys);
}
