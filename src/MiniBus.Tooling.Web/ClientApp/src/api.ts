export type ToolingSourceAvailability = {
  source: string;
  isAvailable: boolean;
  reason?: string | null;
};

export type TimelineFragment = {
  source: string;
  kind: string;
  timestamp: string;
  title: string;
  details: Record<string, string>;
};

export type MessageTimeline = {
  fragments: TimelineFragment[];
  sources: ToolingSourceAvailability[];
};

export type InboxRecord = {
  endpointName: string;
  messageId: string;
  processedUtc: string;
  correlationId?: string | null;
  headers: Record<string, string>;
};

export type OutboxRecord = {
  id: string;
  outgoingMessageId: string;
  endpointName: string;
  incomingMessageId: string;
  operationKind: string;
  messageType: string;
  createdUtc: string;
  attemptCount: number;
  lastErrorSummary?: string | null;
  status: string;
  correlationId?: string | null;
  headers: Record<string, string>;
};

export type SagaRecord = {
  id: string;
  dataType: string;
  correlationId: string;
  createdUtc: string;
  updatedUtc: string;
  isCompleted: boolean;
  completedUtc?: string | null;
  version: string;
  status: string;
};

export type ToolingRecord = InboxRecord | OutboxRecord | SagaRecord;

export async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(path);
  if (!response.ok) {
    throw new Error(await readError(response));
  }

  return response.json() as Promise<T>;
}

async function readError(response: Response): Promise<string> {
  const text = await response.text();
  if (!text) {
    return `${response.status} ${response.statusText}`;
  }

  try {
    const parsed = JSON.parse(text) as { title?: string; detail?: string };
    return parsed.detail ?? parsed.title ?? text;
  } catch {
    return text;
  }
}
