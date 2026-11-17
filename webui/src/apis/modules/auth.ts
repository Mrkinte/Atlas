import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";

type LoginRequest = components["schemas"]["LoginRequest"];
type ChangeUsernameRequest = components["schemas"]["ChangeUsernameRequest"];
type ResetPasswordRequest = components["schemas"]["ResetPasswordRequest"];

export function login(body: LoginRequest) {
  return client.POST("/api/auth/login", { body });
}

export function logout() {
  return client.POST("/api/auth/logout");
}

export function refresh() {
  return client.POST("/api/auth/refresh");
}

export function status() {
  return client.GET("/api/auth/status");
}

export function changeUsername(body: ChangeUsernameRequest) {
  return client.POST("/api/auth/change-username", { body });
}

export function resetPassword(body: ResetPasswordRequest) {
  return client.POST("/api/auth/reset-password", { body });
}
