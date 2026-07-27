import { useCallback, useEffect, useState } from "react";
import "./App.css";
import { getModelStatus } from "./api";
import TrainingPage from "./TrainingPage";
import ChatPage from "./ChatPage";
import { describeError, type ModelTrained, type Tab } from "./shared";

function App() {
  const [activeTab, setActiveTab] = useState<Tab>("train");
  const [modelTrained, setModelTrained] = useState<ModelTrained>(null);
  const [statusError, setStatusError] = useState<string | null>(null);

  const refreshStatus = useCallback(async () => {
    setStatusError(null);
    try {
      const status = await getModelStatus();
      setModelTrained(status.modelTrained);
    } catch (err) {
      setStatusError(describeError(err));
    }
  }, []);

  useEffect(() => {
    refreshStatus();
  }, [refreshStatus]);

  return (
    <div className="app">
      <header className="topbar">
        <div className="wordmark">
          MiniGpt<span className="wordmark-accent">Chat</span>
        </div>

        <nav className="tabs" role="tablist" aria-label="Sections">
          <button
            role="tab"
            aria-selected={activeTab === "train"}
            className={`tab ${activeTab === "train" ? "tab--active" : ""}`}
            onClick={() => setActiveTab("train")}
          >
            Train
          </button>
          <button
            role="tab"
            aria-selected={activeTab === "chat"}
            className={`tab ${activeTab === "chat" ? "tab--active" : ""}`}
            onClick={() => setActiveTab("chat")}
          >
            Chat
          </button>
        </nav>

        <ModelStatusChip modelTrained={modelTrained} error={statusError} />
      </header>

      {/* Both pages stay mounted so switching tabs never loses in-progress
          training output or an open chat session - only the active one is shown. */}
      <main className="stage">
        <section className="page" hidden={activeTab !== "train"}>
          <TrainingPage
            modelTrained={modelTrained}
            onTrained={refreshStatus}
            onSwitchToChat={() => setActiveTab("chat")}
          />
        </section>
        <section className="page" hidden={activeTab !== "chat"}>
          <ChatPage modelTrained={modelTrained} onGoToTrain={() => setActiveTab("train")} />
        </section>
      </main>
    </div>
  );
}

function ModelStatusChip({ modelTrained, error }: { modelTrained: ModelTrained; error: string | null }) {
  const kind = error ? "error" : modelTrained === null ? "checking" : modelTrained ? "ready" : "not-trained";
  const label = error ? "error" : modelTrained === null ? "checking" : modelTrained ? "ready" : "no checkpoint";

  return (
    <div className={`status-chip status-chip--${kind}`}>
      <span className="status-dot" />
      <span className="mono">{label}</span>
    </div>
  );
}

export default App;
