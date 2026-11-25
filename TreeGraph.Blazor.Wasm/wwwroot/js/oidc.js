const pendingKey = 'treegraph.oidc.pending';
const logoutKey = 'treegraph.oidc.logout';
let logoutChannel;
let logoutRef;
let storageHandler;

function base64Url(bytes) {
  let binary = '';
  for (const value of bytes) binary += String.fromCharCode(value);
  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replaceAll('=', '');
}
function randomString(length) {
  const bytes = crypto.getRandomValues(new Uint8Array(length));
  return base64Url(bytes).slice(0, length);
}
function localReturnUrl(value) {
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//') ? value : '/';
}

export async function beginLogin(authority, clientId, redirectUri, returnUrl, maxAge) {
  const state = randomString(43);
  const nonce = randomString(43);
  const verifier = randomString(96);
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  sessionStorage.setItem(pendingKey, JSON.stringify({ state, nonce, codeVerifier: verifier, returnUrl: localReturnUrl(returnUrl) }));
  const query = new URLSearchParams({
    client_id: clientId,
    redirect_uri: redirectUri,
    response_type: 'code',
    scope: 'openid profile email',
    state,
    nonce,
    code_challenge: base64Url(new Uint8Array(digest)),
    code_challenge_method: 'S256'
  });
  if (maxAge !== null && maxAge !== undefined) query.set('max_age', String(maxAge));
  window.location.assign(`${authority}/protocol/openid-connect/auth?${query}`);
}

export function hasPendingLogin() {
  return sessionStorage.getItem(pendingKey) !== null;
}

export function consumeLoginTransaction(state) {
  const raw = sessionStorage.getItem(pendingKey);
  if (!raw) return null;
  sessionStorage.removeItem(pendingKey);
  try {
    const pending = JSON.parse(raw);
    if (pending.state !== state) return null;
    return { codeVerifier: pending.codeVerifier, nonce: pending.nonce, returnUrl: localReturnUrl(pending.returnUrl) };
  } catch {
    return null;
  }
}

export function endLogout(authority, idToken, postLogoutRedirectUri) {
  sessionStorage.removeItem(pendingKey);
  const query = new URLSearchParams({ post_logout_redirect_uri: postLogoutRedirectUri });
  if (idToken) query.set('id_token_hint', idToken);
  window.location.assign(`${authority}/protocol/openid-connect/logout?${query}`);
}

export function broadcastLogout() {
  if (logoutChannel) logoutChannel.postMessage('logout');
  localStorage.setItem(logoutKey, `${Date.now()}:${randomString(8)}`);
  localStorage.removeItem(logoutKey);
}

export function watchLogout(dotNetRef) {
  unwatchLogout();
  logoutRef = dotNetRef;
  const notify = () => logoutRef?.invokeMethodAsync('OnRemoteLogout');
  if ('BroadcastChannel' in window) {
    logoutChannel = new BroadcastChannel('treegraph-identity');
    logoutChannel.addEventListener('message', event => { if (event.data === 'logout') notify(); });
  }
  storageHandler = event => { if (event.key === logoutKey) notify(); };
  window.addEventListener('storage', storageHandler);
}

export function unwatchLogout() {
  logoutChannel?.close();
  logoutChannel = null;
  logoutRef = null;
  if (storageHandler) window.removeEventListener('storage', storageHandler);
  storageHandler = null;
}
