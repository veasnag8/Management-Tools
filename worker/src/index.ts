import { AutoRouter } from 'itty-router';
import { Env } from './types';
import { jsonResponse } from './middleware/validation';
import { handleActivate } from './routes/activate';
import { handleValidate } from './routes/validate';
import { handleHeartbeat } from './routes/heartbeat';
import { handleDeactivate } from './routes/deactivate';
import { handleResetDevice } from './routes/reset-device';
import { handleVersion } from './routes/version';

const router = AutoRouter();

// CORS Headers helper
function handleCors(request: Request): Headers {
  const origin = request.headers.get('Origin') || '*';
  const headers = new Headers();
  headers.set('Access-Control-Allow-Origin', origin);
  headers.set('Access-Control-Allow-Methods', 'GET, POST, PUT, DELETE, OPTIONS');
  headers.set('Access-Control-Allow-Headers', 'Content-Type, Authorization, X-Requested-With');
  headers.set('Access-Control-Max-Age', '86400');
  return headers;
}

// OPTIONS preflight
router.options('*', (request: Request) => {
  return new Response(null, {
    status: 204,
    headers: handleCors(request)
  });
});

// GET /health
router.get('/health', () => {
  return jsonResponse({
    status: 'ok',
    timestamp: new Date().toISOString()
  });
});

// Windows Client Licensing API Routes
router.post('/v1/activate', (request: Request, env: Env) => handleActivate(request, env));
router.post('/v1/validate', (request: Request, env: Env) => handleValidate(request, env));
router.post('/v1/heartbeat', (request: Request, env: Env) => handleHeartbeat(request, env));
router.post('/v1/deactivate', (request: Request, env: Env) => handleDeactivate(request, env));
router.post('/v1/reset-device', (request: Request, env: Env) => handleResetDevice(request, env));
router.get('/v1/version', (request: Request, env: Env) => handleVersion(request, env));

// 404 handler
router.all('*', () => {
  return new Response(
    JSON.stringify({
      success: false,
      data: null,
      error: { code: 'INVALID_REQUEST', message: 'Endpoint not found.' }
    }),
    { status: 404, headers: { 'Content-Type': 'application/json' } }
  );
});

export default {
  async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
    try {
      const response = await router.fetch(request, env, ctx);
      
      // Inject CORS headers into non-OPTIONS responses
      const newHeaders = new Headers(response.headers);
      const origin = request.headers.get('Origin') || '*';
      newHeaders.set('Access-Control-Allow-Origin', origin);
      newHeaders.set('Access-Control-Allow-Headers', 'Content-Type, Authorization, X-Requested-With');
      
      return new Response(response.body, {
        status: response.status,
        statusText: response.statusText,
        headers: newHeaders
      });
    } catch (err: any) {
      console.error('Unhandled Worker error:', err);
      return new Response(
        JSON.stringify({
          success: false,
          data: null,
          error: { code: 'SERVER_ERROR', message: 'Internal Server Error' }
        }),
        { status: 500, headers: { 'Content-Type': 'application/json' } }
      );
    }
  }
};
