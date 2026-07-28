// Small pieces shared between App.tsx, TrainingPage.tsx and ChatPage.tsx. Kept
// in their own file (rather than re-exported from App.tsx) so each page module
// only exports components - keeps Vite/React Fast Refresh working cleanly.

export type Tab = "train" | "chat";

/** null = still checking on first load. */
export type ModelTrained = boolean | null;

export function describeError(err: unknown): string {
  return err instanceof Error ? err.message : String(err);
}

export function StatusMessage({ text }: { text: string }) {
  return (
    <div className="panel status-panel">
      <div className="pulse-ring" aria-hidden="true" />
      <p>{text}</p>
    </div>
  );
}
