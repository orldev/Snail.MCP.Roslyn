# Finding a tool

## <code>find_<wbr>tool</code>

Finds this server's tools for a task described in words, with what each takes, enough to call it at once. Answers the best matches first; when nothing matches, every tool, with isMatched false. Ask it before concluding that the server has no tool for something.

*read-only · repeat-safe · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `task` | `string?` | — | `null` | What you are trying to do, in words: 'change one line', 'push the branch'. |
| `limit` | `integer` | — | `5` | How many tools to name, 1 to 20; five by default. |
| `intent` | `string?` | — | `null` | The same as task, for a caller that names it intent; task wins when both are sent. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "task": {
      "description": "What you are trying to do, in words: \u0027change one line\u0027, \u0027push the branch\u0027.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "limit": {
      "description": "How many tools to name, 1 to 20; five by default.",
      "type": "integer",
      "default": 5
    },
    "intent": {
      "description": "The same as task, for a caller that names it intent; task wins when both are sent.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  }
}
```

</details>

