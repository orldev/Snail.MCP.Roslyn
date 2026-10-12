# The solution: load, diagnostics, files, overview

## <code>load_<wbr>solution</code>

Load a C# solution or project and keep it compiled for later questions. Returns the project list, counts of compiler errors and warnings, and a readiness state. Readiness 'Degraded' means package restore has not run, so semantic answers under-report: report that state instead of trusting an empty result. The 'unloaded' list names .csproj files on disk that the solution does not include and that no answer covers. Call this once per solution before any other tool.

*only adds · every call adds · may reach outside services.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `path` | `string` | yes | — | Absolute path to a .sln, .slnx or .csproj file, or a directory containing one. |
| `countDiagnostics` | `boolean` | — | `false` | Also count compiler errors and warnings. Off by default: it costs a full build and the numbers go stale on the next edit. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "path": {
      "description": "Absolute path to a .sln, .slnx or .csproj file, or a directory containing one.",
      "type": "string"
    },
    "countDiagnostics": {
      "description": "Also count compiler errors and warnings. Off by default: it costs a full build and the numbers go stale on the next edit.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "path"
  ]
}
```

</details>

## <code>get_<wbr>diagnostics</code>

List compiler errors and warnings for a loaded solution, reflecting the files as they are on disk right now. Call it right after editing to see what the change broke; it is cheap enough to use after every edit. Identical diagnostics repeated across generated components are collapsed and counted in 'repeats'. Truncation is always stated, so a short list is not the same as a complete one.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `scope` | `string?` | — | `null` | Optional absolute file or directory path that narrows the results. |
| `minimum` | `Info` \| `Warning` \| `Error` | — | `Warning` | Lowest severity to include: Info, Warning or Error. |
| `limit` | `integer` | — | `50` | Maximum issues to return. |
| `includeGenerated` | `boolean` | — | `false` | Include diagnostics that only exist in generated code the caller cannot edit. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "scope": {
      "description": "Optional absolute file or directory path that narrows the results.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "minimum": {
      "description": "Lowest severity to include: Info, Warning or Error.",
      "type": "string",
      "enum": [
        "Info",
        "Warning",
        "Error"
      ],
      "default": "Warning"
    },
    "limit": {
      "description": "Maximum issues to return.",
      "type": "integer",
      "default": 50
    },
    "includeGenerated": {
      "description": "Include diagnostics that only exist in generated code the caller cannot edit.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>read_<wbr>file</code>

Read one file the way the compiler sees it: the numbered text, the diagnostics that live in it, and every declaration it holds with the identifier the symbol tools expect and a count of its uses across the solution. Prefer this over a plain file read for C#: same text, plus what text cannot show. A .razor or .cshtml file of the solution comes back as a component view: diagnostics and @code declarations mapped to their authored lines, component tags resolved to types, the directives the file declares, and render-cost patterns under SNR ids. A file outside the loaded solution comes back as text alone, and the answer says so.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `file` | `string` | yes | — | Absolute path of the file to read. |
| `startLine` | `integer` | — | `1` | One-based line to start from. |
| `maxLines` | `integer` | — | `2000` | Maximum lines of text to return. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "file": {
      "description": "Absolute path of the file to read.",
      "type": "string"
    },
    "startLine": {
      "description": "One-based line to start from.",
      "type": "integer",
      "default": 1
    },
    "maxLines": {
      "description": "Maximum lines of text to return.",
      "type": "integer",
      "default": 2000
    }
  },
  "required": [
    "solutionId",
    "file"
  ]
}
```

</details>

## <code>get_<wbr>solution_<wbr>overview</code>

Describe the whole solution in one budgeted answer: project graph, sizes, compiler errors, the types the rest of the code names most often, and the structural breaches found. Call this before proposing a restructuring, instead of reading files one by one. Analysis always covers the whole solution; only the answer is budgeted, and truncation is stated.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `limit` | `integer` | — | `20` | Maximum entries per section of the answer. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "limit": {
      "description": "Maximum entries per section of the answer.",
      "type": "integer",
      "default": 20
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>check_<wbr>architecture</code>

List structural breaches: namespaces that do not match their folder, namespaces that depend on each other in a cycle, projects that do, and markup blocks the solution repeats — a repeated block differing only in values is a candidate to extract into a component. Every finding is a claim about the code checkable at the file and line given. Whether a breach matters is a judgement this tool does not make.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `limit` | `integer` | — | `50` | Maximum findings to return. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "limit": {
      "description": "Maximum findings to return.",
      "type": "integer",
      "default": 50
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>health_<wbr>check</code>

Report whether the server itself is in a state where its answers can be trusted: readiness, project and document counts, how long a full diagnostic pass takes, memory in use, how many solutions are loaded, and whether scripting is enabled. Call this when an answer looks suspiciously empty or suspiciously fast.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>unload_<wbr>solution</code>

Release a loaded solution and the memory its compilations hold. Use it when you are done with one solution and about to work on another; loading again is cheap.

*only adds · repeat-safe · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

