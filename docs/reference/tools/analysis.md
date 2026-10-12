# Impact, analyzers, fixes and validation

## <code>analyze_<wbr>change_<wbr>impact</code>

Measure what depends on a symbol before anyone changes it: references grouped by project, the overrides and interface implementations a shape change would break, and the text occurrences the compiler will not protect. Call this before a rename or a signature change, not after.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare name; used only when it matches exactly one symbol. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `limit` | `integer` | — | `50` | Maximum entries per section of the answer. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "symbolId": {
      "description": "Symbol id from search_symbols.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "name": {
      "description": "Bare name; used only when it matches exactly one symbol.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "file": {
      "description": "Absolute file path.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "line": {
      "description": "1-based line.",
      "type": "integer",
      "default": 0
    },
    "column": {
      "description": "1-based column.",
      "type": "integer",
      "default": 0
    },
    "limit": {
      "description": "Maximum entries per section of the answer.",
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

## <code>run_<wbr>analyzers</code>

Run the analyzers over the solution and report what they find, with the category that explains why it matters. Use category 'Performance' for the known-slow patterns worth replacing. This costs seconds, not milliseconds: use get_diagnostics for the after-every-edit loop. If a project references no analyzers, the ones shipped with this server are used and the answer says so.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `scope` | `string?` | — | `null` | Optional absolute file or directory path that narrows the results. |
| `category` | `string?` | — | `null` | Analyzer category to keep, such as Performance, Design, Reliability or Usage. |
| `minimum` | `Info` \| `Warning` \| `Error` | — | `Info` | Lowest severity to include: Info, Warning or Error. |
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
    "scope": {
      "description": "Optional absolute file or directory path that narrows the results.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "category": {
      "description": "Analyzer category to keep, such as Performance, Design, Reliability or Usage.",
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
      "default": "Info"
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

## <code>get_<wbr>code_<wbr>fixes</code>

Return the repairs the compiler's own providers offer for a diagnostic, as edits for you to apply. This tool never writes to disk. A fix that compiles is not automatically a fix that is right.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `file` | `string` | yes | — | Absolute path of the file the diagnostic points at. |
| `line` | `integer` | yes | — | One-based line the diagnostic points at. |
| `diagnosticId` | `string?` | — | `null` | Diagnostic id to fix, such as CS0103; the first diagnostic on the line when omitted. |

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
      "description": "Absolute path of the file the diagnostic points at.",
      "type": "string"
    },
    "line": {
      "description": "One-based line the diagnostic points at.",
      "type": "integer"
    },
    "diagnosticId": {
      "description": "Diagnostic id to fix, such as CS0103; the first diagnostic on the line when omitted.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "solutionId",
    "file",
    "line"
  ]
}
```

</details>

## <code>validate</code>

Compile a file's proposed contents in the context of the solution, before writing anything to disk. Answers what the change would break and what it would repair. Use this when you are about to write a file and the cost of being wrong is a broken build.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `file` | `string` | yes | — | Absolute path the contents are meant for; the file need not exist yet. |
| `content` | `string` | yes | — | The complete proposed contents of that file. |

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
      "description": "Absolute path the contents are meant for; the file need not exist yet.",
      "type": "string"
    },
    "content": {
      "description": "The complete proposed contents of that file.",
      "type": "string"
    }
  },
  "required": [
    "solutionId",
    "file",
    "content"
  ]
}
```

</details>

