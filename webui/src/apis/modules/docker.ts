import { client } from "@/apis/client.ts";

export function getDockerStatus() {
  return client.GET("/api/docker/status");
}
