// Thin wrapper around the ChatBot.Api HTTP endpoints (see
// ChatBot.Api/Controllers). Keeping every fetch call in one place means the
// rest of the app never has to think about URLs, headers, or JSON parsing.

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5141";

export interface ModelStatus {
  modelTrained: boolean;
}

export interface TrainOptions {
  steps?: number;
  batchSize?: number;
  learningRate?: number;
  /** How often (in steps) the backend emits a loss log line. */
  logEveryNSteps?: number;
  /** A local server-side file path (only set when Azure Blob Storage isn't configured - see uploadDataset). */
  dataPath?: string;
  /** A blob name in the "data" container (only set when Azure Blob Storage is configured - see uploadDataset). */
  datasetBlobName?: string;
}

export interface TrainResult {
  message: string;
  steps: number;
  batchSize: number;
  learningRate: number;
}

export interface TrainingJobStatus {
  status: "running" | "completed" | "failed";
  logs: string[];
  nextCursor: number;
  errorMessage: string | null;
  result: TrainResult | null;
}

export interface UploadDatasetResult {
  /** A blob name (if storedInBlob) or a local server-side file path (if not). */
  datasetPath: string;
  /** True if the file went straight to Azure Blob Storage; false if it was saved locally instead. */
  storedInBlob: boolean;
  fileName: string;
  characterCount: number;
}

/** Problem Details shape ASP.NET Core's `Problem()` helper returns on errors. */
interface ProblemDetails {
  title?: string;
  detail?: string;
}

async function parseJsonOrThrow<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let message = `Request failed with status ${response.status}`;
    try {
      const problem = (await response.json()) as ProblemDetails;
      message = problem.detail ?? problem.title ?? message;
    } catch {
      // Response body wasn't JSON (or was empty) - fall back to the generic message.
    }
    throw new Error(message);
  }
  return (await response.json()) as T;
}

export async function getModelStatus(): Promise<ModelStatus> {
  const response = await fetch(`${API_BASE_URL}/api/model/status`);
  return parseJsonOrThrow<ModelStatus>(response);
}

/**
 * Uploads a custom training text file. Returns a reference to pass back when starting
 * training - as `datasetBlobName` if `storedInBlob` is true, or as `dataPath` otherwise.
 */
export async function uploadDataset(file: File): Promise<UploadDatasetResult> {
  const formData = new FormData();
  formData.append("file", file);

  const response = await fetch(`${API_BASE_URL}/api/dataset`, {
    method: "POST",
    body: formData,
  });
  return parseJsonOrThrow<UploadDatasetResult>(response);
}

/**
 * Starts training in the background and returns immediately with a job id -
 * poll `getTrainingJobStatus` with it to watch progress and find out when it's done.
 */
export async function startTraining(options: TrainOptions): Promise<string> {
  const response = await fetch(`${API_BASE_URL}/api/training`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      steps: options.steps,
      batchSize: options.batchSize,
      learningRate: options.learningRate,
      logEveryNSteps: options.logEveryNSteps,
      dataPath: options.dataPath,
      datasetBlobName: options.datasetBlobName,
    }),
  });
  const data = await parseJsonOrThrow<{ jobId: string }>(response);
  return data.jobId;
}

/** Fetches a training job's status plus any log lines produced since `since` (pass back the previous `nextCursor`). */
export async function getTrainingJobStatus(jobId: string, since: number): Promise<TrainingJobStatus> {
  const response = await fetch(`${API_BASE_URL}/api/training/${jobId}/status?since=${since}`);
  return parseJsonOrThrow<TrainingJobStatus>(response);
}

export async function startChatSession(): Promise<string> {
  const response = await fetch(`${API_BASE_URL}/api/chat/sessions`, { method: "POST" });
  const data = await parseJsonOrThrow<{ sessionId: string }>(response);
  return data.sessionId;
}

export async function sendChatMessage(sessionId: string, message: string): Promise<string> {
  const response = await fetch(`${API_BASE_URL}/api/chat/sessions/${sessionId}/messages`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ message }),
  });
  const data = await parseJsonOrThrow<{ reply: string }>(response);
  return data.reply;
}
