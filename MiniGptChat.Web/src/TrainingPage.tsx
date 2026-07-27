import { useEffect, useRef, useState } from "react";
import {
  getTrainingJobStatus,
  startTraining,
  uploadDataset,
  type TrainResult,
  type UploadDatasetResult,
} from "./api";
import { describeError, type ModelTrained } from "./shared";
import RangeSlider from "./RangeSlider";

interface TrainingPageProps {
  modelTrained: ModelTrained;
  /** Called after a successful training run so the shared status (and Chat tab) can refresh. */
  onTrained: () => void;
  onSwitchToChat: () => void;
}

type Phase = "idle" | "training" | "done" | "error";

// Training requires an explicit choice - either an uploaded file or the built-in
// sample dataset - so "unselected" (nothing chosen yet) is distinct from "sample"
// (the user actively picked the sample dataset). Only "unselected" blocks training.
type DatasetChoice = { kind: "unselected" } | { kind: "sample" } | { kind: "uploaded"; result: UploadDatasetResult };

const STEP_PROGRESS_PATTERN = /step\s+(\d+)\s*\/\s*(\d+)/;

// A curated set rather than a raw continuous slider - a linear drag over the
// tiny range learning rates actually live in (1e-5 .. 1e-2) is too imprecise
// to be useful, so the slider instead steps through common values.
const LEARNING_RATES = [0.00005, 0.0001, 0.0003, 0.0005, 0.001, 0.003, 0.005, 0.01];
const DEFAULT_LEARNING_RATE_INDEX = LEARNING_RATES.indexOf(0.0003);

function TrainingPage({ modelTrained, onTrained, onSwitchToChat }: TrainingPageProps) {
  const [steps, setSteps] = useState(1000);
  const [batchSize, setBatchSize] = useState(32);
  const [learningRateIndex, setLearningRateIndex] = useState(DEFAULT_LEARNING_RATE_INDEX);
  const [logEveryNSteps, setLogEveryNSteps] = useState(100);
  const [datasetChoice, setDatasetChoice] = useState<DatasetChoice>({ kind: "unselected" });

  const [phase, setPhase] = useState<Phase>("idle");
  const [logs, setLogs] = useState<string[]>([]);
  const [result, setResult] = useState<TrainResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  const pollHandle = useRef<{ cancelled: boolean } | null>(null);
  const learningRate = LEARNING_RATES[learningRateIndex];

  useEffect(() => {
    // Stop polling if the page unmounts mid-training (tab switches keep the
    // component mounted, but this guards against a real unmount regardless).
    return () => {
      if (pollHandle.current) pollHandle.current.cancelled = true;
    };
  }, []);

  async function handleTrain() {
    if (datasetChoice.kind === "unselected") return; // Train button is disabled in this state too

    setPhase("training");
    setLogs([]);
    setError(null);
    setResult(null);

    try {
      const uploaded = datasetChoice.kind === "uploaded" ? datasetChoice.result : null;
      const jobId = await startTraining({
        steps,
        batchSize,
        learningRate,
        logEveryNSteps,
        // Which field to send depends on where uploadDataset() actually put the file -
        // a blob name when Azure Blob Storage is configured, a local path otherwise.
        // Both stay undefined for an explicit "sample" choice, which is what tells the
        // backend to fall back to the built-in sample dataset.
        datasetBlobName: uploaded?.storedInBlob ? uploaded.datasetPath : undefined,
        dataPath: uploaded && !uploaded.storedInBlob ? uploaded.datasetPath : undefined,
      });
      pollJob(jobId);
    } catch (err) {
      setError(describeError(err));
      setPhase("error");
    }
  }

  function pollJob(jobId: string) {
    const handle = { cancelled: false };
    pollHandle.current = handle;
    let cursor = 0;

    const tick = async () => {
      if (handle.cancelled) return;
      try {
        const status = await getTrainingJobStatus(jobId, cursor);
        cursor = status.nextCursor;
        if (status.logs.length > 0) {
          setLogs((prev) => [...prev, ...status.logs]);
        }

        if (status.status === "completed") {
          setResult(status.result);
          setPhase("done");
          onTrained();
          return;
        }
        if (status.status === "failed") {
          setError(status.errorMessage ?? "Training failed.");
          setPhase("error");
          return;
        }
        setTimeout(tick, 700);
      } catch (err) {
        setError(describeError(err));
        setPhase("error");
      }
    };

    tick();
  }

  const progress = parseProgress(logs);
  const disabled = phase === "training";
  const datasetMissing = datasetChoice.kind === "unselected";

  return (
    <div className="panel config-sheet">
      <p className="eyebrow">{modelTrained ? "Checkpoint ready" : "Setup required"}</p>
      <h2>{modelTrained ? "Retrain the model" : "Train a model first"}</h2>
      <p className="config-sheet-copy">
        {modelTrained
          ? "Retraining overwrites the current checkpoint. Chat picks up the new weights on its next message."
          : "Train once here, then switch to the Chat tab to see how it does."}{" "}
      </p>

      <DatasetUploader choice={datasetChoice} onChoice={setDatasetChoice} disabled={disabled} />

      <div className="slider-grid">
        <RangeSlider label="steps" value={steps} min={100} max={5000} step={100} onChange={setSteps} disabled={disabled} />
        <RangeSlider
          label="eval interval"
          value={logEveryNSteps}
          min={10}
          max={300}
          step={10}
          onChange={setLogEveryNSteps}
          disabled={disabled}
        />
        <RangeSlider
          label="batch size"
          value={batchSize}
          min={1}
          max={128}
          step={1}
          onChange={setBatchSize}
          disabled={disabled}
        />
        <RangeSlider
          label="learning rate"
          value={learningRateIndex}
          min={0}
          max={LEARNING_RATES.length - 1}
          step={1}
          onChange={setLearningRateIndex}
          disabled={disabled}
          format={() => learningRate.toExponential(1)}
        />
      </div>

      <button className="btn-primary" onClick={handleTrain} disabled={disabled || datasetMissing}>
        {disabled ? "Training…" : modelTrained ? "Retrain" : "Train now"}
      </button>
      {datasetMissing && phase !== "training" && (
        <p className="config-sheet-copy">Choose a training file, or use the sample dataset, before training.</p>
      )}

      {(phase === "training" || logs.length > 0) && (
        <TrainingConsole logs={logs} progress={progress} isRunning={disabled} />
      )}

      {phase === "done" && result && (
        <div className="train-result">
          <p className="mono train-result-line">
            done — {result.steps} steps · batch {result.batchSize} · lr {result.learningRate}
          </p>
          <button className="btn-secondary" onClick={onSwitchToChat}>
            Test it in Chat →
          </button>
        </div>
      )}

      {phase === "error" && <p className="error-text error-text--inline">{error}</p>}
    </div>
  );
}

function DatasetUploader({
  choice,
  onChoice,
  disabled,
}: {
  choice: DatasetChoice;
  onChoice: (choice: DatasetChoice) => void;
  disabled: boolean;
}) {
  const [phase, setPhase] = useState<"idle" | "uploading" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = ""; // allow re-selecting the same file later
    if (!file) return;

    setPhase("uploading");
    setError(null);
    try {
      const uploaded = await uploadDataset(file);
      onChoice({ kind: "uploaded", result: uploaded });
      setPhase("idle");
    } catch (err) {
      setError(describeError(err));
      setPhase("error");
    }
  }

  return (
    <div className="dataset-uploader">
      <span className="config-label">training data</span>
      <div className="dataset-row">
        <div className="dataset-info">
          {choice.kind === "uploaded" ? (
            <span className="mono dataset-name" title={choice.result.fileName}>
              {choice.result.fileName} · {choice.result.characterCount.toLocaleString()} chars
            </span>
          ) : choice.kind === "sample" ? (
            <span className="dataset-name dataset-name--default">built-in sample_conversations.txt</span>
          ) : (
            <span className="dataset-name dataset-name--default">no dataset chosen yet</span>
          )}
        </div>
        <div className="dataset-actions">
          <button
            type="button"
            className="btn-secondary btn-small"
            onClick={() => inputRef.current?.click()}
            disabled={disabled || phase === "uploading"}
          >
            {phase === "uploading" ? "Uploading…" : "Upload file"}
          </button>
          {choice.kind !== "sample" && (
            <button
              type="button"
              className="btn-ghost btn-small"
              onClick={() => onChoice({ kind: "sample" })}
              disabled={disabled}
            >
              Use sample data
            </button>
          )}
        </div>
      </div>
      <input ref={inputRef} type="file" accept=".txt,text/plain" onChange={handleFileChange} hidden />
      {phase === "error" && <p className="error-text error-text--inline">{error}</p>}
    </div>
  );
}

function TrainingConsole({
  logs,
  progress,
  isRunning,
}: {
  logs: string[];
  progress: number | null;
  isRunning: boolean;
}) {
  const endRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    endRef.current?.scrollIntoView({ block: "end" });
  }, [logs]);

  return (
    <div className="console">
      <div className="console-titlebar">
        <div className="console-dots">
          <span className="console-dot console-dot--red" />
          <span className="console-dot console-dot--yellow" />
          <span className="console-dot console-dot--green" />
        </div>
        <span className="console-title">training.log</span>
        {isRunning ? (
          <span className="console-live">
            <span className="console-live-dot" /> live
          </span>
        ) : (
          <span className="console-live console-live--done">done</span>
        )}
      </div>
      {progress !== null && (
        <div
          className="console-progress"
          role="progressbar"
          aria-valuenow={progress}
          aria-valuemin={0}
          aria-valuemax={100}
        >
          <div className="console-progress-fill" style={{ width: `${progress}%` }} />
        </div>
      )}
      <div className="console-body mono">
        {logs.length === 0 ? (
          <div className="console-line console-empty">waiting for the first log line…</div>
        ) : (
          logs.map((line, index) => (
            <div key={index} className="console-line">
              {line}
            </div>
          ))
        )}
        {isRunning && <div className="console-line console-line--cursor">▌</div>}
        <div ref={endRef} />
      </div>
    </div>
  );
}

function parseProgress(logs: string[]): number | null {
  for (let i = logs.length - 1; i >= 0; i--) {
    const match = STEP_PROGRESS_PATTERN.exec(logs[i]);
    if (match) {
      const [, current, total] = match;
      return Math.min(100, Math.round((Number(current) / Number(total)) * 100));
    }
  }
  return null;
}

export default TrainingPage;
