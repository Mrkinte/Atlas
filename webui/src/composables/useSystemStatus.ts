import { onBeforeUnmount, ref, watch } from "vue";
import { settings } from "@/stores/settings.ts";

export interface SystemStatusDevice {
  hardwareId: string;
  hardwareName: string;
  cpuUsagePercent: number | null;
  memoryTotalBytes: number | null;
  memoryUsedBytes: number | null;
  processWorkingSetBytes: number;
  managedHeapBytes: number;
  diskTotalBytes: number | null;
  diskFreeBytes: number | null;
  loadAverage1m: number | null;
  uptimeSeconds: number | null;
}

interface SystemStatusMessage {
  type: "status" | "denied" | "disabled";
  timestamp: string;
  devices: SystemStatusDevice[];
  reason?: string | null;
}

export type SystemStatusState = "connecting" | "online" | "denied" | "disabled" | "offline";

const WS_PATH = "/api/system-status/ws";
const INITIAL_RETRY_MS = 1000;
const MAX_RETRY_MS = 30000;

/** 订阅系统状态 WebSocket：断线自动重连，硬件未公开时停止重试 */
export function useSystemStatus() {
  const state = ref<SystemStatusState>("connecting");
  const devices = ref<SystemStatusDevice[]>([]);
  const updatedAt = ref<Date | null>(null);

  let socket: WebSocket | null = null;
  let retryTimer: number | undefined;
  let retryDelay = INITIAL_RETRY_MS;
  let stopped = false;

  function clearRetry() {
    if (retryTimer === undefined) return;
    window.clearTimeout(retryTimer);
    retryTimer = undefined;
  }

  function scheduleReconnect() {
    if (stopped || state.value === "denied" || state.value === "disabled") return;

    clearRetry();
    retryTimer = window.setTimeout(() => {
      retryTimer = undefined;
      connect();
    }, retryDelay);
    retryDelay = Math.min(retryDelay * 2, MAX_RETRY_MS);
  }

  function handleMessage(event: MessageEvent<string>) {
    let message: SystemStatusMessage;
    try {
      message = JSON.parse(event.data) as SystemStatusMessage;
    } catch {
      return;
    }

    if (message.type === "denied") {
      state.value = "denied";
      devices.value = [];
      clearRetry();
      return;
    }

    if (message.type === "disabled") {
      state.value = "disabled";
      devices.value = [];
      clearRetry();
      return;
    }

    if (message.type !== "status") return;

    devices.value = message.devices;
    updatedAt.value = new Date(message.timestamp);
    retryDelay = INITIAL_RETRY_MS;

    if (message.devices.length > 0) {
      state.value = "online";
    } else {
      state.value = "denied";
    }
  }

  function connect() {
    if (stopped) return;

    clearRetry();
    state.value = state.value === "offline" ? "connecting" : state.value;

    const protocol = location.protocol === "https:" ? "wss:" : "ws:";
    const url = `${protocol}//${location.host}${WS_PATH}`;

    try {
      socket = new WebSocket(url);
    } catch {
      state.value = "offline";
      scheduleReconnect();
      return;
    }

    socket.onmessage = handleMessage;
    socket.onerror = () => {
      // 重连交给 onclose 处理
    };
    socket.onclose = () => {
      socket = null;
      if (state.value === "denied" || state.value === "disabled") return;

      state.value = "offline";
      scheduleReconnect();
    };
  }

  function stop() {
    stopped = true;
    clearRetry();
    socket?.close();
    socket = null;
  }

  onBeforeUnmount(stop);

  // 设置页切换开关后立即反映到卡片：关闭时断开并停止重连，重新开启时自动连接
  watch(
    () => settings.systemStatusEnabled,
    (enabled) => {
      if (!enabled) {
        clearRetry();
        socket?.close();
        socket = null;
        devices.value = [];
        state.value = "disabled";
        return;
      }

      if (stopped || socket) return;

      retryDelay = INITIAL_RETRY_MS;
      state.value = "connecting";
      connect();
    },
  );

  if (settings.systemStatusEnabled) {
    connect();
  } else {
    state.value = "disabled";
  }

  return { state, devices, updatedAt, stop };
}
