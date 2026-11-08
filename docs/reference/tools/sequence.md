# Several tools in one call

## <code>run_<wbr>tools</code>

Runs several of this server's tools in one call, in order, stopping at the first that fails. For steps that do not depend on reading the previous answer. Each step is an ordinary call of that tool under the same grant; a tool that runs other tools is not a step. An answer too long for the call is left out and its step flagged isOmitted: call that tool alone to read it.

*may overwrite or delete · every call adds · may reach outside services.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `steps` | `array` | yes | — | The steps in order: {"tool": "read_file", "arguments": {"path": "src/App.cs"}}. find_tool says what a tool takes. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "steps": {
      "description": "The steps in order: {\u0022tool\u0022: \u0022read_file\u0022, \u0022arguments\u0022: {\u0022path\u0022: \u0022src/App.cs\u0022}}. find_tool says what a tool takes.",
      "type": "array",
      "items": {
        "type": "object",
        "properties": {
          "tool": {
            "description": "The tool\u0027s name, as the tool list or find_tool gives it.",
            "type": [
              "string",
              "null"
            ]
          },
          "arguments": {
            "description": "The tool\u0027s arguments, as a direct call of the tool would send them.",
            "type": [
              "object",
              "null"
            ],
            "default": null
          }
        },
        "required": [
          "tool"
        ]
      }
    }
  },
  "required": [
    "steps"
  ]
}
```

</details>

