import { client } from "@/apis/client.ts";
import type { components } from "@/apis/schema";

type CreateCardRequest = components["schemas"]["CreateCardRequest"];
type UpdateCardRequest = components["schemas"]["UpdateCardRequest"];

export function listCards() {
  return client.GET("/api/cards");
}

export function createCard(body: CreateCardRequest) {
  return client.POST("/api/cards", { body });
}

export function updateCard(cardId: string, body: UpdateCardRequest) {
  return client.PUT("/api/cards/{cardId}", {
    params: { path: { cardId } },
    body,
  });
}

export function deleteCard(cardId: string) {
  return client.DELETE("/api/cards/{cardId}", {
    params: { path: { cardId } },
  });
}

export function deleteAllCards() {
  return client.DELETE("/api/cards");
}
