import { useEffect, useRef, useState } from "react";
import { sendChatMessage, startChatSession } from "./api";
import { describeError, StatusMessage, type ModelTrained } from "./shared";
import type { ChatMessage } from "./types";

interface ChatPageProps {
  modelTrained: ModelTrained;
  onGoToTrain: () => void;
}

function ChatPage({ modelTrained, onGoToTrain }: ChatPageProps) {
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [sessionError, setSessionError] = useState<string | null>(null);
  const hasStartedSession = useRef(false);

  useEffect(() => {
    if (modelTrained && !hasStartedSession.current) {
      hasStartedSession.current = true;
      startChatSession()
        .then(setSessionId)
        .catch((err) => setSessionError(describeError(err)));
    }
  }, [modelTrained]);

  if (modelTrained === null) {
    return <StatusMessage text="Checking for a trained checkpoint…" />;
  }

  if (modelTrained === false) {
    return (
      <div className="panel gate-panel">
        <p className="eyebrow">Not ready</p>
        <h2>No trained checkpoint yet</h2>
        <p className="config-sheet-copy">
          Train a model on the <strong>Train</strong> tab first, then come back here to test how it does.
        </p>
        <button className="btn-primary" onClick={onGoToTrain}>
          Go to Train
        </button>
      </div>
    );
  }

  if (sessionError) {
    return (
      <div className="panel error-panel">
        <p className="error-title">Couldn't start a chat session</p>
        <p className="mono error-detail">{sessionError}</p>
        <button
          className="btn-primary"
          onClick={() => {
            hasStartedSession.current = false;
            setSessionError(null);
          }}
        >
          Try again
        </button>
      </div>
    );
  }

  if (!sessionId) {
    return <StatusMessage text="Starting a chat session…" />;
  }

  return <ChatWindow sessionId={sessionId} />;
}

function ChatWindow({ sessionId }: { sessionId: string }) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const transcriptEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    transcriptEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages, isSending]);

  // The input is disabled while a message is in flight (see below), which drops
  // focus - bring it back once it's re-enabled so the user can keep typing
  // without reaching for the mouse/tab key.
  useEffect(() => {
    if (!isSending) inputRef.current?.focus();
  }, [isSending]);

  async function handleSend() {
    const trimmed = input.trim();
    if (trimmed.length === 0 || isSending) return;

    setMessages((prev) => [...prev, { role: "user", text: trimmed }]);
    setInput("");
    setIsSending(true);
    setError(null);

    try {
      const reply = await sendChatMessage(sessionId, trimmed);
      setMessages((prev) => [...prev, { role: "bot", text: reply }]);
    } catch (err) {
      setError(describeError(err));
    } finally {
      setIsSending(false);
    }
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === "Enter") {
      handleSend();
    }
  }

  return (
    <div className="chat-window">
      <div className="transcript">
        {messages.length === 0 && (
          <div className="empty-state">
            <p>Say hi to get started.</p>
            <p className="mono session-id">session {sessionId.slice(0, 8)}</p>
          </div>
        )}
        {messages.map((message, index) => (
          <div key={index} className={`bubble ${message.role}`}>
            {message.text}
          </div>
        ))}
        {isSending && (
          <div className="bubble bot typing" aria-label="Bot is responding">
            <span />
            <span />
            <span />
          </div>
        )}
        <div ref={transcriptEndRef} />
      </div>

      {error && <p className="error-text">{error}</p>}

      <div className="composer">
        <input
          ref={inputRef}
          type="text"
          placeholder="> type a message"
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          disabled={isSending}
          className="mono"
        />
        <button className="btn-primary" onClick={handleSend} disabled={isSending || input.trim().length === 0}>
          Send
        </button>
      </div>
    </div>
  );
}

export default ChatPage;
