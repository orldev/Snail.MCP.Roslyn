# Scripts over the whole solution

## <code>run_<wbr>script</code>

Evaluate a C# script against the loaded solution and return its value and printed output. Use it for the question no fixed tool anticipates: one pass over the whole solution, aggregated the way you need. Available globals: Solution, Compilations, Print(value), Types(pattern), Sym(id), Refs(symbol), Impls(symbol), Callers(symbol). The script cannot change the code: the workspace is an in-memory snapshot and edits to it are discarded.

*may overwrite or delete · every call adds · may reach outside services.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `code` | `string` | yes | — | C# script body; its last expression is the returned value. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "code": {
      "description": "C# script body; its last expression is the returned value.",
      "type": "string"
    }
  },
  "required": [
    "solutionId",
    "code"
  ]
}
```

</details>

