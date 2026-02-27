 # VikunjaBridge

An API bridge that converts free-text input into structured [Vikunja](https://vikunja.io/) tasks using an LLM (Together.ai with Llama 3.3 70B).

## How It Works

```
Free text → Prepare → Ask (LLM) → Clean → Transform → Load → Vikunja tasks
```

1. **Prepare** — Fetches projects, labels, and sample tasks from Vikunja to build a context-rich LLM prompt.
2. **Ask** — Sends the prompt to Together.ai to extract structured task data.
3. **Clean** — Parses the raw LLM response (handles markdown fences and other quirks).
4. **Transform** — Validates project/label IDs against actual Vikunja data, with fallbacks.
5. **Load** — Creates tasks in Vikunja and bulk-assigns labels.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- A running [Vikunja](https://vikunja.io/) instance with an API token
- A [Together.ai](https://www.together.ai/) API key

## Configuration

Copy `appsettings.json` and fill in your values in `appsettings.Development.json`:

| Setting              | Description                              |
| -------------------- | ---------------------------------------- |
| `Vikunja:BaseUrl`    | URL of your Vikunja instance             |
| `Vikunja:ApiToken`   | Vikunja API token                        |
| `TogetherAi:ApiKey`  | Together.ai API key                      |
| `TogetherAi:Model`   | LLM model (default: `meta-llama/Llama-3.3-70B-Instruct-Turbo`) |
| `Api:ApiKey`         | Key clients must send in `X-Api-Key` header |

## Running

```bash
# Development
dotnet run

# Production (Docker)
docker build -t vikunja-bridge .
docker run -p 8080:8080 \
  -e Vikunja__BaseUrl="https://your-vikunja.example.com" \
  -e Vikunja__ApiToken="tk_..." \
  -e TogetherAi__ApiKey="..." \
  -e Api__ApiKey="..." \
  vikunja-bridge
```

## API

### `GET /health`

Returns `"Healthy"`. No authentication required.

### `POST /add-task`

Parses free text and creates one or more tasks in Vikunja.

**Headers:**

| Header      | Required | Description        |
| ----------- | -------- | ------------------ |
| `X-Api-Key` | Yes      | Your bridge API key |

**Request body:**

```json
{
  "freetext": "Buy groceries tomorrow and schedule dentist appointment for next Monday"
}
```

**Response (200):**

```json
{
  "message": "Created 2 task(s)",
  "tasks": [
    {
      "taskId": 42,
      "title": "Buy groceries",
      "projectId": 1,
      "labelsAdded": 0,
      "error": null
    },
    {
      "taskId": 43,
      "title": "Schedule dentist appointment",
      "projectId": 1,
      "labelsAdded": 1,
      "error": null
    }
  ]
}
```

## Project Structure

```
├── Program.cs          # App setup, DI, and endpoint definitions
├── vikunja.cs          # DTOs and Refit interface for the Vikunja API
├── Transform.cs        # Validates & maps LLM output to Vikunja tasks
├── Load.cs             # Creates tasks and assigns labels in Vikunja
├── ai_call/
│   ├── prepare.cs      # Fetches Vikunja context & builds LLM prompt
│   ├── ask.cs          # Calls Together.ai chat completions API
│   └── clean.cs        # Parses/sanitizes raw LLM JSON response
├── Dockerfile          # Multi-stage build (SDK → ASP.NET runtime)
└── appsettings.json    # Configuration template
```

## Tech Stack

- **ASP.NET Core 9** — Minimal API
- **Refit** — Type-safe HTTP client for the Vikunja API
- **Together.ai** — LLM inference (Llama 3.3 70B Instruct Turbo)
