import { ref } from "vue";
import { client } from "@/apis/client.ts";
import {
  logout as logoutApi,
  refresh as refreshApi,
  status as statusApi,
} from "@/apis/modules/auth.ts";
import type { components } from "@/apis/schema";

type LoginResponse = components["schemas"]["LoginResponse"];

/** 以后端 /api/auth/status 为准 */
export const isAuthenticated = ref(false);
export const currentUser = ref<string | null>(null);
/** ISO 字符串，来自登录/刷新响应 */
export const expiresAt = ref<string | null>(null);

const REFRESH_LEAD_MS = 60 * 1000;
const RETRY_DELAY_MS = 30 * 1000;
/** 避免过期时间已到时立即触发 */
const MIN_DELAY_MS = 5 * 1000;
/** 刷新令牌轮换后旧值失效，并发刷新会互相踢下线 */
const REFRESH_LOCK = "atlas-auth-refresh";
const AUTH_CHANNEL = "atlas-auth";
/** 仅缓存过期时间戳，不涉及凭证 */
const EXPIRES_KEY = "atlas.auth.expiresAt";

let refreshTimer: number | undefined;
let refreshing: Promise<boolean> | null = null;
let channel: BroadcastChannel | null = null;
let started = false;

type AuthMessage =
  { type: "session"; username: string | null; expiresAt: string | null } | { type: "logout" };

function readStoredExpiry(): string | null {
  return localStorage.getItem(EXPIRES_KEY);
}

function persistExpiry(value: string | null) {
  expiresAt.value = value;
  if (value) {
    localStorage.setItem(EXPIRES_KEY, value);
  } else {
    localStorage.removeItem(EXPIRES_KEY);
  }
}

function postMessage(message: AuthMessage) {
  channel?.postMessage(message);
}

export function applySession(data: LoginResponse, broadcast = false) {
  isAuthenticated.value = true;
  currentUser.value = data.username ?? null;
  persistExpiry(data.expiresAt ?? null);
  scheduleRefresh();
  if (broadcast) {
    postMessage({
      type: "session",
      username: currentUser.value,
      expiresAt: expiresAt.value,
    });
  }
}

export function clearSession(broadcast = false) {
  isAuthenticated.value = false;
  currentUser.value = null;
  persistExpiry(null);
  window.clearTimeout(refreshTimer);
  if (broadcast) postMessage({ type: "logout" });
  window.dispatchEvent(new Event("auth:expired"));
}

export async function logoutSession(): Promise<void> {
  try {
    await logoutApi();
  } catch {
    // 网络异常也要清理本地态；失效 Cookie 由服务端在过期后自然淘汰
  }
  clearSession(true);
}

// 带 body 的请求发送后无法重放，预存未消费副本；Request 由 GC 回收，无需清理
const replayableRequests = new WeakMap<Request, Request>();

// 业务接口 401 的唯一收敛点：先续期再重放原请求，续期失败才清理会话
// /api/auth/* 除外：登录失败、旧密码错误的 401 不代表会话失效
client.use({
  onRequest({ request }) {
    if (request.method !== "GET" && request.method !== "HEAD") {
      replayableRequests.set(request, request.clone());
    }
  },
  async onResponse({ request, response }) {
    if (response.status !== 401 || request.url.includes("/api/auth/")) {
      return response;
    }
    // 续期成功后重放，调用方无感知
    if (isAuthenticated.value && (await refreshSession())) {
      const replayable = replayableRequests.get(request) ?? (request.bodyUsed ? null : request);
      if (replayable) {
        const retried = await fetch(replayable);
        if (retried.status !== 401) return retried;
      }
    }
    // 未登录或续期后仍 401
    clearSession(true);
    return response;
  },
});

function scheduleRefresh() {
  window.clearTimeout(refreshTimer);
  const expire = expiresAt.value ? Date.parse(expiresAt.value) : NaN;
  if (!Number.isFinite(expire)) return;

  const delay = Math.max(MIN_DELAY_MS, expire - Date.now() - REFRESH_LEAD_MS);
  refreshTimer = window.setTimeout(() => {
    void refreshSession();
  }, delay);
}

function scheduleRetry() {
  window.clearTimeout(refreshTimer);
  if (!isAuthenticated.value) return;

  refreshTimer = window.setTimeout(() => {
    void refreshSession();
  }, RETRY_DELAY_MS);
}

async function withRefreshLock(task: () => Promise<boolean>): Promise<boolean> {
  if (!("locks" in navigator)) return task();
  return await navigator.locks.request(REFRESH_LOCK, task);
}

/** 同标签页单飞、跨标签页加锁，避免刷新令牌被并发轮换后互相失效 */
export function refreshSession(): Promise<boolean> {
  refreshing ??= withRefreshLock(refreshOnce).finally(() => {
    refreshing = null;
  });
  return refreshing;
}

async function refreshOnce(): Promise<boolean> {
  // 拿到锁时其他标签页可能刚刷新完，沿用其新过期时间
  const shared = readStoredExpiry();
  const sharedTime = shared ? Date.parse(shared) : NaN;
  const ownTime = expiresAt.value ? Date.parse(expiresAt.value) : NaN;
  const sharedFresh = Number.isFinite(sharedTime) && sharedTime - Date.now() > REFRESH_LEAD_MS;
  const ownFresh = Number.isFinite(ownTime) && ownTime - Date.now() > REFRESH_LEAD_MS;
  if (sharedFresh && (!ownFresh || sharedTime > ownTime + 1000)) {
    isAuthenticated.value = true;
    persistExpiry(shared);
    scheduleRefresh();
    return true;
  }

  try {
    const { data, response } = await refreshApi();
    if (!response.ok || !data) {
      // 刷新令牌已失效，后端同时清除了 Cookie
      if (response.status === 401) {
        clearSession(true);
        return false;
      }
      // 服务端异常保留登录态稍后重试
      scheduleRetry();
      return isAuthenticated.value;
    }

    applySession(data, true);
    return true;
  } catch {
    scheduleRetry();
    return isAuthenticated.value;
  }
}

/** 网络异常无法确认时按未登录处理，避免误入受保护页面 */
export async function refreshAuthStatus(): Promise<boolean> {
  try {
    const { data, response } = await statusApi();
    if (!response.ok || !data) {
      isAuthenticated.value = false;
      currentUser.value = null;
      return false;
    }
    isAuthenticated.value = data.isAuthenticated === true;
    currentUser.value = isAuthenticated.value ? (data.username ?? null) : null;
    return isAuthenticated.value;
  } catch {
    isAuthenticated.value = false;
    currentUser.value = null;
    return false;
  }
}

/** 访问令牌临近过期时先续期，避免进页面后接口立即 401 */
export async function ensureSession(): Promise<boolean> {
  if (await refreshAuthStatus()) {
    const expire = expiresAt.value ? Date.parse(expiresAt.value) : NaN;
    if (!Number.isFinite(expire) || expire - Date.now() <= REFRESH_LEAD_MS) {
      // 临近过期先续期，避免进页面后接口立即 401
      return refreshSession();
    }
    scheduleRefresh();
    return true;
  }
  return refreshSession();
}

/** 后台标签页的定时器会被节流，回到前台时补一次刷新 */
function refreshIfNeeded() {
  if (!isAuthenticated.value) return;
  const expire = expiresAt.value ? Date.parse(expiresAt.value) : NaN;
  if (!Number.isFinite(expire) || expire - Date.now() <= REFRESH_LEAD_MS * 2) {
    void refreshSession();
  }
}

export function startAuthSync() {
  if (started) return;
  started = true;

  if ("BroadcastChannel" in window) {
    channel = new BroadcastChannel(AUTH_CHANNEL);
    channel.onmessage = (event: MessageEvent<AuthMessage>) => {
      const message = event.data;
      if (message.type === "session") {
        // Cookie 由所有标签页共享，同步状态即可
        isAuthenticated.value = true;
        currentUser.value = message.username;
        persistExpiry(message.expiresAt);
        scheduleRefresh();
      } else {
        isAuthenticated.value = false;
        currentUser.value = null;
        persistExpiry(null);
        window.clearTimeout(refreshTimer);
      }
    };
  }

  window.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") refreshIfNeeded();
  });
  window.addEventListener("pageshow", refreshIfNeeded);

  // 冷启动先用本地过期时间恢复定时器，再向后端确认会话
  const stored = readStoredExpiry();
  persistExpiry(stored);
  if (stored) scheduleRefresh();
  void ensureSession();
}
