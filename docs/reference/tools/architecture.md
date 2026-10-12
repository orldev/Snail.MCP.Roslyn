# Registrations, unused and untested code

## <code>get_<wbr>di_<wbr>registrations</code>

List the dependency injection registrations found in source: service, implementation and lifetime, with the line that declares them. Also names the interfaces that are implemented but registered nowhere. A registration written as a string or resolved by reflection is invisible to rename_symbol: this is where you check what a rename left behind.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `service` | `string?` | — | `null` | Part of a service type name, to narrow the list. |
| `limit` | `integer` | — | `100` | Maximum registrations to return. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "service": {
      "description": "Part of a service type name, to narrow the list.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "limit": {
      "description": "Maximum registrations to return.",
      "type": "integer",
      "default": 100
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>find_<wbr>unused_<wbr>symbols</code>

List declarations that no code in the loaded solution references. That is not the same as dead: reflection, injection by name and serialisation are invisible to the compiler. Blazor markup is counted through the generated code, so an unused [Parameter] or @inject appears here with its authored file and line. Treat every finding as a candidate to verify, never as a licence to delete.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `scope` | `string?` | — | `null` | Optional absolute file or directory path that narrows the audit. |
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
      "description": "Optional absolute file or directory path that narrows the audit.",
      "type": [
        "string",
        "null"
      ],
      "default": null
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

## <code>find_<wbr>uncovered_<wbr>symbols</code>

List public declarations that no test project in this solution references. Referencing is not testing, but zero references from tests is a hard signal that text search cannot produce.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `scope` | `string?` | — | `null` | Optional absolute file or directory path that narrows the audit. |
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
      "description": "Optional absolute file or directory path that narrows the audit.",
      "type": [
        "string",
        "null"
      ],
      "default": null
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

## <code>find_<wbr>tests_<wbr>for_<wbr>symbol</code>

List the test methods that reach this symbol, so you know what will catch a change to it before you make one.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare name; used only when it matches exactly one symbol. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `limit` | `integer` | — | `50` | Maximum test methods to return. |

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
      "description": "Maximum test methods to return.",
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

