import type { ProtocolMode } from "@/cards/ServiceCard/config.ts";

export function isValidHttpUrl(value: string): boolean {
  try {
    const url = new URL(value.trim());
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

/** 反代子路径前缀，根路径返回空串 */
function getBasePath(): string {
  const path = window.location.pathname.replace(/\/+$/, "");
  return path === "" || path === "/" ? "" : path;
}

/** 反代子路径下优先拼路径，端口访问时优先拼端口 */
export function resolveAutoServiceUrl(
  port?: number | null,
  proxyPath?: string,
  protocolMode?: ProtocolMode,
): string {
  const { hostname } = window.location;
  const protocol = resolveProtocol(protocolMode);
  const portUrl = port ? `${protocol}//${hostname}:${port}` : "";
  const pathUrl = proxyPath ? `${protocol}//${hostname}${proxyPath}` : "";

  return getBasePath() ? pathUrl || portUrl : portUrl || pathUrl;
}

/** 未设置时沿用当前页面协议 */
function resolveProtocol(mode?: ProtocolMode): string {
  if (mode === "https") return "https:";
  if (mode === "http") return "http:";
  return window.location.protocol;
}
