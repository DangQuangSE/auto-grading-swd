import { buildGradingPrompt, type GradingPromptInput } from "./gradingPrompt.ts";

export type OpenRouterResult = {
  model: string;
  content: string;
  metadata: Record<string, unknown>;
  rawResponse: unknown;
};

export async function requestOpenRouterGrading(input: GradingPromptInput): Promise<OpenRouterResult> {
  const apiKey = Deno.env.get("OPENROUTER_API_KEY");
  const model = Deno.env.get("OPENROUTER_MODEL") ?? "deepseek/deepseek-chat";

  if (!apiKey) {
    throw new Error("OPENROUTER_API_KEY is required.");
  }

  const response = await fetch("https://openrouter.ai/api/v1/chat/completions", {
    method: "POST",
    headers: {
      Authorization: `Bearer ${apiKey}`,
      "Content-Type": "application/json",
      "HTTP-Referer": Deno.env.get("APP_PUBLIC_URL") ?? "http://localhost:5173",
      "X-Title": "Auto Grading",
    },
    body: JSON.stringify({
      model,
      messages: buildGradingPrompt(input),
      temperature: 0.1,
      response_format: { type: "json_object" },
    }),
  });

  const rawResponse = await response.json();

  if (!response.ok) {
    throw new Error(`OpenRouter request failed: ${JSON.stringify(rawResponse)}`);
  }

  const content = rawResponse?.choices?.[0]?.message?.content;
  if (typeof content !== "string") {
    throw new Error("OpenRouter response did not include message content.");
  }

  return {
    model,
    content,
    rawResponse,
    metadata: {
      id: rawResponse.id,
      usage: rawResponse.usage,
      provider: rawResponse.provider,
    },
  };
}
