import createClient from "openapi-fetch";
import type { paths } from "@/apis/schema";

export const client = createClient<paths>();
