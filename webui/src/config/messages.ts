export const NETWORK_ERROR = "网络错误";

export const SETTINGS_SAVE_FAILED = "设置保存失败";

/**
 * 会话失效文案，全站唯一定义；
 * 模块层收到 401 且 client 中间件续期失败时使用
 */
export const UNAUTHORIZED_MESSAGE = "请先登录";

/** 与后端 demo-readonly ProblemDetails 的 detail 保持一致 */
export const DEMO_READONLY_MESSAGE = "演示模式：修改不会保存";
