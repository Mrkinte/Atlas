export type Live2dFeedbackLevel = "success" | "info";

type Live2dFeedbackHandler = (message: string, level: Live2dFeedbackLevel) => boolean;

let handler: Live2dFeedbackHandler | undefined;

export function registerLive2dFeedback(fn: Live2dFeedbackHandler) {
  handler = fn;
  return () => {
    if (handler === fn) handler = undefined;
  };
}

/** 未注册或无法显示时返回 false */
export function showLive2dFeedback(message: string, level: Live2dFeedbackLevel): boolean {
  return handler?.(message, level) ?? false;
}
