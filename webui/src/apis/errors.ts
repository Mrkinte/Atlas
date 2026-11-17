import { UNAUTHORIZED_MESSAGE } from "@/config/messages.ts";

/** RFC 7807 响应体 */
type ProblemDetails = {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
};

/**
 * 401 响应体为空，只能按状态码判断；
 * detail 为 null 表示后端本就无文案，回退调用方文案
 */
export function extractErrorMessage(error: unknown, fallback: string, status?: number): string {
  if (status === 401) return UNAUTHORIZED_MESSAGE;

  if (error && typeof error === "object") {
    const detail = (error as ProblemDetails).detail;
    if (typeof detail === "string" && detail.trim()) return detail;
  }
  if (typeof error === "string" && error.trim()) return error;

  return fallback;
}
