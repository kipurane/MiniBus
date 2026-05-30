import React, { FormEvent, useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import {
  getJson,
  InboxRecord,
  MessageTimeline,
  OutboxRecord,
  SagaRecord,
  ToolingRecord
} from "./api";
import "./styles.css";

type ViewName = "inbox" | "outbox" | "sagas";

const views: ViewName[] = ["inbox", "outbox", "sagas"];

function App() {
  const [view, setView] = useState<ViewName>("inbox");
  const [records, setRecords] = useState<ToolingRecord[]>([]);
  const [selected, setSelected] = useState<ToolingRecord | null>(null);
  const [timelineQuery, setTimelineQuery] = useState("");
  const [timeline, setTimeline] = useState<MessageTimeline | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    setSelected(null);
    getJson<ToolingRecord[]>(`/api/tooling/${view}`)
      .then(setRecords)
      .catch((reason: Error) => setError(reason.message));
  }, [view]);

  const title = useMemo(() => view.charAt(0).toUpperCase() + view.slice(1), [view]);

  async function searchTimeline(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const query = timelineQuery.trim();
    if (!query) {
      return;
    }

    setError(null);
    try {
      setTimeline(await getJson<MessageTimeline>(`/api/tooling/timeline/correlation/${encodeURIComponent(query)}`));
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Timeline request failed.");
    }
  }

  return (
    <main className="shell">
      <aside className="sidebar">
        <div className="brand">MiniBus</div>
        <nav aria-label="Tooling views">
          {views.map((name) => (
            <button
              className={name === view ? "active" : ""}
              key={name}
              onClick={() => setView(name)}
              type="button"
            >
              {name}
            </button>
          ))}
        </nav>
      </aside>

      <section className="workspace">
        <header className="toolbar">
          <div>
            <p className="eyebrow">Read-only operational tooling</p>
            <h1>{title}</h1>
          </div>
          <form className="timeline-search" onSubmit={searchTimeline}>
            <input
              aria-label="Correlation id"
              onChange={(event) => setTimelineQuery(event.target.value)}
              placeholder="Correlation id"
              value={timelineQuery}
            />
            <button type="submit">Timeline</button>
          </form>
        </header>

        {error && <div className="notice">{error}</div>}

        <div className="content">
          <section className="list" aria-label={`${title} records`}>
            {records.map((record, index) => (
              <button key={recordKey(record, index)} onClick={() => setSelected(record)} type="button">
                <strong>{recordTitle(record)}</strong>
                <span>{recordSubtitle(record)}</span>
              </button>
            ))}
          </section>

          <section className="detail">
            {selected ? <RecordDetail record={selected} /> : <p>Select a record to inspect details.</p>}
          </section>
        </div>

        {timeline && <TimelineView timeline={timeline} />}
      </section>
    </main>
  );
}

function RecordDetail({ record }: { record: ToolingRecord }) {
  return <pre>{JSON.stringify(record, null, 2)}</pre>;
}

function TimelineView({ timeline }: { timeline: MessageTimeline }) {
  return (
    <section className="timeline">
      <h2>Timeline</h2>
      <div className="source-row">
        {timeline.sources.map((source) => (
          <span
            className={source.isAvailable ? "source available" : "source unavailable"}
            key={source.source}
            title={source.reason ?? undefined}
          >
            {source.source}
          </span>
        ))}
      </div>
      {timeline.fragments.map((fragment) => (
        <article key={`${fragment.source}-${fragment.timestamp}-${fragment.title}`}>
          <time>{fragment.timestamp}</time>
          <strong>{fragment.title}</strong>
          <span>{fragment.kind}</span>
        </article>
      ))}
    </section>
  );
}

function recordKey(record: ToolingRecord, index: number): string {
  if (isInbox(record)) {
    return record.messageId;
  }

  if (isOutbox(record)) {
    return record.outgoingMessageId;
  }

  return `${record.correlationId}-${index}`;
}

function recordTitle(record: ToolingRecord): string {
  if (isInbox(record)) {
    return record.messageId;
  }

  if (isOutbox(record)) {
    return record.outgoingMessageId;
  }

  return record.correlationId;
}

function recordSubtitle(record: ToolingRecord): string {
  if (isInbox(record)) {
    return record.endpointName;
  }

  if (isOutbox(record)) {
    return `${record.status} · ${record.endpointName}`;
  }

  return `${record.status} · ${record.dataType}`;
}

function isInbox(record: ToolingRecord): record is InboxRecord {
  return "processedUtc" in record;
}

function isOutbox(record: ToolingRecord): record is OutboxRecord {
  return "outgoingMessageId" in record;
}

createRoot(document.getElementById("root")!).render(<App />);
