# MiniGptChat

A tiny decoder-only Transformer ("mini-GPT") chatbot, trained **completely
from scratch** in C# — no pretrained weights, no external LLM APIs. It's a
learning project for understanding how a GPT-style model actually works
under the hood: tokenization, embeddings, causal self-attention, training
via next-token prediction, and autoregressive sampling — plus how to wrap
that model in a console app, an HTTP API, and a web front end.

Everything runs locally on your CPU using [TorchSharp](https://github.com/dotnet/TorchSharp)
(the .NET bindings for libtorch/PyTorch).

## What this is (and isn't)

This model is **small** and trained on a **small** built-in dataset (a few
hundred short "User / Bot" exchanges). That's intentional — the goal is to
see the whole pipeline work end-to-end quickly on a laptop CPU, not to build
something that rivals a real chatbot. Expect:

- Simple, sometimes repetitive or nonsensical replies.
- A very limited "personality" that only knows about what's in the sample data.
- Occasional garbled text, especially early in training or with a small model.

This is expected and fine — the point is to *see the mechanics working*, not
to get production-quality answers. If you want better replies, train longer,
use a larger model, and/or add more of your own training data (see below).

## Solution structure

The solution is split into four projects, each with a distinct job:

```
MiniGptChat.slnx

MiniGptChat/            Class library - the model, training, and chat engine
MiniGptChat.Cli/        Console app - `dotnet run -- train` / `dotnet run -- chat`
MiniGptChat.Api/        ASP.NET Core Web API (controllers) - HTTP endpoints
MiniGptChat.Web/        React + TypeScript + Vite - browser chat UI

Data/                   Shared training text + saved checkpoint (see below)
```

`MiniGptChat.Cli` and `MiniGptChat.Api` both reference the `MiniGptChat`
library and use its exact same services — they're just two different front
doors onto the same model. `MiniGptChat.Web` talks to `MiniGptChat.Api` over
HTTP; it has no direct dependency on the .NET code at all.

### Why a shared `Data` folder works across projects

`MiniGptChat.Cli` and `MiniGptChat.Api` run from different working
directories and build to different output folders. So that training via one
project is immediately visible to the other (rather than each ending up with
its own separate copy of the checkpoint), `GptConfig`'s default file paths
are resolved by `MiniGptChat/RepoPaths.cs`, which walks up from the running
assembly's location to find `MiniGptChat.slnx` and anchors the `Data` folder
there. This means training via the CLI, the API, or the sample dataset itself
are all reading/writing the exact same three files:

| File | Contents |
|---|---|
| `Data/model.dat` | The trained model's weights |
| `Data/vocab.json` | The character ↔ id vocabulary mapping |
| `Data/model_config.json` | The architecture (embed dim, layers, heads, block size, vocab size) used to rebuild the model shape before loading the weights |

## Tech stack

- **.NET 10** (SDK required) for `MiniGptChat`, `MiniGptChat.Cli`, `MiniGptChat.Api`
- [TorchSharp](https://www.nuget.org/packages/TorchSharp) — managed API for building/training the model
- [libtorch-cpu](https://www.nuget.org/packages/libtorch-cpu) — the native CPU-only backend TorchSharp calls into
- [Microsoft.Extensions.DependencyInjection](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection) — wires up the library's services, shared by the CLI and the API
- ASP.NET Core Web API with **controllers** (`[ApiController]`/`ControllerBase`), not minimal API endpoints
- **React 19 + TypeScript + Vite** for the web UI (Node.js required), with self-hosted fonts via `@fontsource` (no CDN calls)

## Installing dependencies

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and
[Node.js](https://nodejs.org/) (for the web UI) installed. NuGet packages
(TorchSharp, libtorch-cpu, DI, ASP.NET Core) are restored automatically the
first time you build:

```bash
dotnet restore
dotnet build MiniGptChat.slnx
```

`libtorch-cpu` is a large native package (several hundred MB) containing the
actual tensor math engine; the first restore will take a little while to
download it.

For the web UI:

```bash
cd MiniGptChat.Web
npm install
```

## How to train

**Option A — console:**

```bash
cd MiniGptChat.Cli
dotnet run -- train
dotnet run -- train --steps 5000 --batch-size 64 --lr 0.0003   # optional overrides
```

**Option B — HTTP API:** training runs as a background job so you can poll
for progress instead of holding one request open for several minutes.

```bash
cd MiniGptChat.Api
dotnet run
# in another terminal:
curl -X POST http://localhost:5141/api/training \
     -H "Content-Type: application/json" \
     -d '{"steps": 3000}'
# => 202 Accepted, {"jobId":"..."}

curl "http://localhost:5141/api/training/<jobId>/status?since=0"
# => {"status":"running","logs":["Loading training text...", "..."],"nextCursor":4,"errorMessage":null,"result":null}
# poll again with since=<nextCursor> to get only the new lines; status becomes
# "completed" (with a populated "result") or "failed" (with "errorMessage") when done.
```

To train on your own text instead of the built-in sample, upload it first and
pass the returned path as `dataPath`:

```bash
curl -X POST http://localhost:5141/api/dataset -F "file=@my-conversations.txt"
# => {"datasetPath":"...\\Data\\uploaded_dataset.txt","fileName":"my-conversations.txt","characterCount":12345}

curl -X POST http://localhost:5141/api/training \
     -H "Content-Type: application/json" \
     -d '{"steps": 3000, "dataPath": "...\\Data\\uploaded_dataset.txt"}'
```

**Option C — web UI:** open the **Train** tab. Optionally upload your own
`.txt` file (or leave it on the built-in sample), adjust the steps / eval
interval / batch size / learning rate sliders, and click **Train now** (or
**Retrain**). A terminal-style console panel streams the same log lines the
CLI prints — loading the data, vocab size, then a loss line every "eval
interval" steps — with a progress bar and a live/done indicator, so you can
watch training happen instead of staring at a spinner.

Every option reads `Data/sample_conversations.txt` (or your uploaded file),
builds a character vocabulary from it, trains a fresh model with next-token
prediction, and saves `model.dat` / `vocab.json` / `model_config.json` into
the shared `Data/` folder. Training itself is still fully synchronous and
CPU-only under the hood — a few thousand steps with the default small model
typically takes several minutes on a modern laptop — the API just runs it on
a background thread (see `MiniGptChat.Api/Services/TrainingJobService.cs`)
instead of blocking the HTTP request, and `ITrainingService.Train` accepts an
optional `Action<string>? onLog` callback so both the API's job log and the
console's stdout get the same lines.

## How to chat

**Option A — console:**

```bash
cd MiniGptChat.Cli
dotnet run -- chat
```

```
You: hi
Bot: Hello! How can I help you today?
You: what is your name
Bot: I'm a mini chatbot built from scratch in C#.
```

Type `exit` or `quit` to leave.

**Option B — HTTP API directly:**

```bash
curl -X POST http://localhost:5141/api/chat/sessions
# => {"sessionId":"..."}

curl -X POST http://localhost:5141/api/chat/sessions/<sessionId>/messages \
     -H "Content-Type: application/json" \
     -d '{"message":"hi"}'
# => {"sessionId":"...","reply":"Hello! How can I help you today?"}
```

**Option C — web UI:**

```bash
# terminal 1
cd MiniGptChat.Api
dotnet run --launch-profile http     # http://localhost:5141

# terminal 2
cd MiniGptChat.Web
npm run dev                          # http://localhost:5173
```

Open `http://localhost:5173`. The app has two tabs:

- **Train** — configure steps/batch size/learning rate and train (or retrain)
  the model. When it finishes, a **Test it in Chat →** button switches you
  straight to the Chat tab.
- **Chat** — if no checkpoint exists yet, this tab tells you to train first
  (with a button back to the Train tab) instead of erroring; once a
  checkpoint exists, a session starts automatically so you can try out the
  model and judge how coherent its replies are.

Switching tabs never loses your place — both pages stay mounted, so
in-progress training output and an open chat conversation are both preserved
if you flip back and forth. Retraining from the Train tab also invalidates
the API's cached in-memory model (see `ChatSessionService.InvalidateModel()`),
so the Chat tab picks up the newly trained weights on its very next message
instead of silently continuing to talk to the old ones.

Every option formats the running conversation into the same
`User: ...\nBot: ...` pattern the model was trained on, trimming older turns
once things grow longer than the model's context window (`BlockSize`) — the
console app does this in-process (`ConversationHistory`), and the API does it
per-session server-side (`ChatSessionService`), so the browser client never
needs to resend the whole transcript.

## The web UI (`MiniGptChat.Web`)

A small, considered design rather than default component-library styling:

- **Palette** — a warm paper neutral (light) / deep instrument-panel ink
  (dark), both paired with a single signal-teal accent standing in for "the
  model is responding" (see the token system at the top of `src/index.css`).
  Both themes are hand-tuned, not a naive inversion.
- **Type** — Fraunces (display serif) for the wordmark and headings only,
  Hanken Grotesk for body/UI text, and JetBrains Mono reserved for anything
  that's literally a number the model produced (session id, hyperparameter
  values) — all self-hosted via `@fontsource`, no font CDN calls, consistent
  with the project's "nothing phones out" ethos.
- **Layout** — a slim top bar (wordmark, a Train/Chat tab switcher, and a
  live status chip showing checking/ready/no-checkpoint/error) above a single
  page surface. The empty chat transcript carries a faint dot-grid watermark
  (a nod to sampling/character tokenization); the training panel is a config
  sheet with a file uploader and slider controls (see below) rather than a
  generic form.
- **Sliders, not number boxes** — steps, eval interval, batch size and
  learning rate are all drag sliders (`RangeSlider.tsx`) with the value shown
  in bold mono next to the label. Learning rate steps through a curated list
  of common values (5e-5 … 1e-2) rather than a raw continuous range, since
  dragging over a range that small is too imprecise to be useful.
- **A real console window for training logs** — `TrainingConsole` (inside
  `TrainingPage.tsx`) looks like a terminal: a titlebar with traffic-light
  dots and a "● live" / "done" indicator, a thin progress bar parsed from the
  latest `step N/Total` log line, and a scrolling, always-dark log body with
  a blinking cursor while running. It's a deliberately single-theme
  component (stays dark in both light and dark app themes) — the metaphor is
  "you're watching a process run," which reads best staying consistently dark.
- Small deliberate motion: bubbles ease in on arrival, the "bot is
  responding" indicator is a three-dot signal blink instead of a literal
  "...", and the status chip pulses while checking. All animation respects
  `prefers-reduced-motion`.

**Files:**

```
App.tsx           Shell: top bar, Train/Chat tab switcher, shared model-status polling
TrainingPage.tsx  Upload, sliders, train/retrain, the live TrainingConsole, "Test it in Chat" CTA
RangeSlider.tsx   Reusable labeled slider (label left, mono value right, teal-filled track)
ChatPage.tsx      Gates on model status; owns the session + message transcript
shared.tsx        Types and small pieces used by more than one page (kept separate
                   so each page file only exports components, for clean Fast Refresh)
api.ts            The only file that knows about HTTP - typed wrappers around every
                   endpoint below, so the pages only ever call getModelStatus(),
                   uploadDataset(), startTraining(), getTrainingJobStatus(),
                   startChatSession(), sendChatMessage()
```

Both pages stay mounted at all times (`App.tsx` toggles them with the
`hidden` attribute rather than conditionally rendering), so switching tabs
never loses an in-progress training run's state or an open chat session.

## API reference

All endpoints are under `http://localhost:5141` (or `https://localhost:7292`)
by default; see `MiniGptChat.Api/Properties/launchSettings.json`.

| Method & path | Body | Response |
|---|---|---|
| `GET /api/model/status` | — | `{ "modelTrained": bool }` |
| `POST /api/dataset` | multipart/form-data, field `file` | `{ "datasetPath", "fileName", "characterCount" }` (400 if missing/empty) |
| `POST /api/training` | `{ "steps"?, "batchSize"?, "learningRate"?, "logEveryNSteps"?, "dataPath"? }` (all optional) | 202 Accepted, `{ "jobId" }` |
| `GET /api/training/{jobId}/status?since=N` | — | `{ "status", "logs", "nextCursor", "errorMessage", "result" }` (404 if unknown job) |
| `POST /api/chat/sessions` | — | `{ "sessionId" }` (409 if no trained model yet) |
| `POST /api/chat/sessions/{sessionId}/messages` | `{ "message" }` | `{ "sessionId", "reply" }` (404 if unknown session) |

OpenAPI/Swagger metadata is available at `/openapi/v1.json` in Development.

CORS is enabled for `http://localhost:5173` (Vite's default dev port) so the
web UI can call the API directly from the browser.

## Using your own data

Three ways to do this, all equivalent:

- **CLI**: `dotnet run -- train --data path/to/your.txt`
- **API/curl**: `POST /api/dataset` (see above) then pass its `datasetPath` as `dataPath` to `POST /api/training`
- **Web UI**: click **Upload file** on the Train tab

The only expectation is the same `User: ...` / `Bot: ...` line-pair format
used in `Data/sample_conversations.txt` — the model has no built-in notion of
turns, it just learns whatever pattern is in the text. More data and more
varied conversations will generally produce better (if still simple) replies.
Uploads always land at `Data/uploaded_dataset.txt` (overwriting any previous
upload), so there's exactly one "the custom dataset" at a time; click **Use
sample data** on the Train tab to go back to the built-in dataset.

## Azure Blob Storage (optional, `MiniGptChat.Api` only)

By default everything lives on local disk in `Data/` (see "Solution
structure" above). `MiniGptChat.Api` can optionally sync that folder with two
containers in an Azure Storage account — **data** (training text files) and
**checkpoint** (`model.dat` / `vocab.json` / `model_config.json`) — so a
trained model survives beyond one machine's disk. This is entirely
optional: `MiniGptChat.Cli` never touches Azure, and the API itself falls
back to pure local file storage whenever it isn't configured.

**Setup:** set a connection string under `AzureStorage:ConnectionString` in
`MiniGptChat.Api/appsettings.Development.json` (or any other .NET
configuration source — an environment variable
`AzureStorage__ConnectionString`, `dotnet user-secrets`, etc.):

```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

**⚠️ Keep this out of source control.** `appsettings.json` ships with an
empty placeholder for exactly this reason — put the real value in
`appsettings.Development.json` (or an environment variable) instead, and
treat that file as sensitive. An account key is a bearer credential: anyone
who has it can read/write/delete everything in the storage account. If a key
is ever pasted somewhere it shouldn't be (a chat log, a public repo, a
screenshot), regenerate it from the Azure Portal (**Storage account →
Security + networking → Access keys → Rotate key**) rather than assuming
it's fine because "it's just a dev container."

**What happens when it's configured:**

- On startup, the API creates the `data` and `checkpoint` containers if they
  don't exist, downloads `model.dat` / `vocab.json` / `model_config.json`
  from `checkpoint` into the local `Data/` folder if present (so a fresh
  clone/machine/container can chat immediately without retraining), and
  uploads `sample_conversations.txt` to `data` the first time.
- `POST /api/dataset` uploads the file to the `data` container in addition
  to saving it locally.
- After a training job finishes, its checkpoint files are uploaded to the
  `checkpoint` container (you'll see this as log lines in the same training
  console the web UI shows: "Uploading checkpoint to Azure Blob Storage...").

See `MiniGptChat.Api/Services/IBlobStorageService.cs` / `BlobStorageService.cs`
for the implementation — every method is a no-op when `IsEnabled` is false,
which is exactly what happens when no connection string is set.

## Project structure (inside `MiniGptChat/`)

Small, single-purpose services wired together with a dependency injection
container (`ServiceRegistration.cs`), shared by both `MiniGptChat.Cli` and
`MiniGptChat.Api`:

```
GptConfig.cs                    All hyperparameters and file paths in one place
RepoPaths.cs                    Locates the shared Data/ folder at the solution root

Tokenization/
  CharTokenizer.cs               Pure encode/decode logic over a char<->id vocabulary
  ITokenizerService.cs           Contract for building/saving/loading a tokenizer
  CharTokenizerService.cs        Builds a vocab from text; reads/writes vocab.json

Model/
  MiniGptModel.cs                Token + positional embeddings -> N blocks -> logits
  TransformerBlock.cs            One block: attention + feed-forward, pre-norm + residual
  CausalSelfAttention.cs         Multi-head masked self-attention
  FeedForward.cs                 The per-token MLP sub-layer
  IModelService.cs               Contract for creating models and (de)serializing checkpoints
  ModelService.cs                Creates MiniGptModel instances; reads/writes weights + config

Corpus/
  ITrainingDataProvider.cs       Contract for loading raw training text
  FileTrainingDataProvider.cs    Reads the training text file from disk

Training/
  ITrainingService.cs            Contract for training a model end to end
  TrainingService.cs             The training loop: batches -> loss -> backprop -> save
  IBatchSampler.cs                Contract for drawing training batches
  RandomBatchSampler.cs          Picks random (input, target) windows from the corpus

Generation/
  GenerationOptions.cs           Temperature / top-k / max length / stop marker settings
  ITextGenerationService.cs      Contract for autoregressive generation
  TextGenerationService.cs       Samples new tokens one at a time from the model

Chat/
  ChatModelContext.cs             Bundles a loaded model + tokenizer + config + options
  IChatModelLoader.cs / ChatModelLoader.cs     Loads a checkpoint into a ChatModelContext
  IChatReplyService.cs / ChatReplyService.cs   Produces one reply given history + a message
  ConversationHistory.cs         Tracks/trims the running transcript fed back into the model
  IChatService.cs / ChatService.cs             Console chat loop (used by MiniGptChat.Cli)
  IChatSessionService.cs / ChatSessionService.cs   Multi-session chat (used by MiniGptChat.Api)

ServiceRegistration.cs           IServiceCollection extension registering every service above
```

`MiniGptChat.Cli/Program.cs` builds its own `ServiceProvider` from this
registration; `MiniGptChat.Api/Program.cs` calls the same
`AddMiniGptChatServices()` extension from its own ASP.NET Core DI setup — so
both entry points are wired up identically and neither duplicates the
loading/generation logic.

`MiniGptChat.Api` itself adds API-only services on top of the library:

```
Controllers/
  ModelController.cs      GET api/model/status
  TrainingController.cs   POST api/training (starts a job), GET .../status (polls it)
  DatasetController.cs    POST api/dataset (saves an upload to Data/uploaded_dataset.txt)
  ChatController.cs       POST api/chat/sessions, POST api/chat/sessions/{id}/messages

Services/
  ITrainingJobService.cs / TrainingJobService.cs
    Runs ITrainingService.Train on a background thread per job, capturing every
    onLog line into an in-memory, per-job log list that GetStatus() reads from
    (with a cursor so repeated polls only return new lines). On success it also
    calls IChatSessionService.InvalidateModel() so Chat picks up the new weights,
    then uploads the checkpoint via IBlobStorageService if Azure is configured.
  IBlobStorageService.cs / BlobStorageService.cs / BlobStorageOptions.cs
    Optional sync with Azure Blob Storage (see "Azure Blob Storage" above).
    Every method no-ops when no connection string is configured.
```

## Architecture: how the model works

This is a **decoder-only Transformer**, the same family of architecture used
by GPT-style models, just much smaller:

1. **Tokenizer** (`Tokenization/CharTokenizer.cs`): character-level, no
   external tokenizer library. The vocabulary is just every distinct
   character seen in the training text.
2. **Embeddings** (`Model/MiniGptModel.cs`): each token id is looked up in a
   learned token embedding table; a separate learned positional embedding is
   added so the model knows *where* in the sequence each character is
   (attention has no built-in sense of order).
3. **Transformer blocks** (`Model/TransformerBlock.cs`), stacked `NumLayers`
   times, each containing:
   - **Multi-head causal self-attention** (`Model/CausalSelfAttention.cs`) —
     every position can look at itself and earlier positions (never the
     future, enforced with a triangular mask), splitting the computation
     across several attention "heads" that can each focus on different
     relationships.
   - **Feed-forward MLP** (`Model/FeedForward.cs`) — a per-position
     expand → GELU → project transformation that gives the model extra
     capacity beyond just mixing information between positions.
   - Both sub-layers are wrapped in **residual connections** (`x = x +
     subLayer(x)`) with **layer normalization** applied beforehand
     (pre-norm), which is what makes it practical to stack multiple blocks
     without training becoming unstable.
4. **Output head**: a final layer norm followed by a linear layer that
   projects back to vocabulary size, producing one score ("logit") per
   possible next character at every position.

## Hyperparameters (`GptConfig.cs`)

| Setting | Default | What it controls |
|---|---|---|
| `EmbedDim` | 96 | Size of each token's vector representation. Bigger = more capacity, slower training. |
| `NumLayers` | 3 | How many Transformer blocks are stacked. |
| `NumHeads` | 4 | How many parallel attention heads per block (`EmbedDim` must be divisible by this). |
| `BlockSize` | 128 | The context window: how many previous characters the model can consider at once. |
| `Dropout` | 0.1 | Fraction of activations randomly zeroed during training, as regularization against overfitting. |
| `FeedForwardMultiplier` | 4 | The feed-forward MLP's hidden size, as a multiple of `EmbedDim`. |
| `TrainingSteps` | 3000 | Number of optimizer (Adam) update steps. |
| `BatchSize` | 32 | How many training sequences are processed per step. |
| `LearningRate` | 3e-4 | Adam's step size — how much weights move per update. |
| `LogEveryNSteps` | 100 | How often the current loss is printed during training. |

`VocabSize` isn't set by hand — it's filled in automatically from however
many distinct characters the tokenizer finds in your training text.

Generation-time settings live in `Generation/GenerationOptions.cs`:

| Setting | Default | What it controls |
|---|---|---|
| `Temperature` | 0.8 | Randomness of sampling. Lower = more predictable/repetitive, higher = more random. |
| `TopK` | 20 | Only the K most likely next characters are considered at each step. `0` disables this filter. |
| `MaxNewTokens` | 200 | Hard cap on reply length. |
| `EndMarker` | `"\n"` | Generation stops as soon as this string is produced (each bot reply in the training data is one line, so a newline naturally ends a reply). |

## Notes on training loss

The loss printed during training is cross-entropy over next-character
prediction. A freshly initialized model with ~55 vocabulary characters starts
around `ln(55) ≈ 4.0` (random guessing). As training progresses you should
see it drop — a loss in the low 1.x–2.x range on this small dataset generally
starts producing recognizably word-like, sometimes on-topic replies; it won't
reach the very low losses of models trained on huge datasets, and that's
expected for a dataset this size.
