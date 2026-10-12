# Refactoring plans

## <code>extract_<wbr>component</code>

Plan the extraction of a markup block from a .razor file into a new component. This tool never writes: it returns the new file's content as an edit and the page-side change as a compact replacement — delete the given line range, put the replacement text in its place. You supply the judgment — the line range, 'substitutions' renaming page-private names to parameter names, the 'code' block declaring them, and the 'tag' that replaces the block; the tool supplies the guarantees: the range must not cut an element apart (checked by the Razor parser), and the candidate is compiled in context so 'unresolved' names every identifier that stops resolving outside the page. Call it first without substitutions to learn the leaks; while 'unresolved' is non-empty the plan carries no edits. Then call again with the full judgment, apply, and run get_diagnostics.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `file` | `string` | yes | — | Absolute path of the .razor file to extract from. |
| `startLine` | `integer` | yes | — | One-based first line of the block, inclusive. |
| `endLine` | `integer` | yes | — | One-based last line of the block, inclusive. |
| `name` | `string` | yes | — | Name of the new component; also names the new file. |
| `substitutions` | `object?` | — | `null` | Renames applied to the block, page-private name to parameter name; longest keys first. Omit on the first call to learn the leaks. |
| `code` | `string?` | — | `null` | The @code block of the new component, verbatim, declaring its [Parameter]s. |
| `tag` | `string?` | — | `null` | Markup that replaces the block in the source file; a self-closing tag of the new component when omitted. |
| `newFile` | `string?` | — | `null` | Absolute path for the new file; a sibling named after the component when omitted. |

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
      "description": "Absolute path of the .razor file to extract from.",
      "type": "string"
    },
    "startLine": {
      "description": "One-based first line of the block, inclusive.",
      "type": "integer"
    },
    "endLine": {
      "description": "One-based last line of the block, inclusive.",
      "type": "integer"
    },
    "name": {
      "description": "Name of the new component; also names the new file.",
      "type": "string"
    },
    "substitutions": {
      "description": "Renames applied to the block, page-private name to parameter name; longest keys first. Omit on the first call to learn the leaks.",
      "type": [
        "object",
        "null"
      ],
      "additionalProperties": {
        "type": [
          "string",
          "null"
        ]
      },
      "default": null
    },
    "code": {
      "description": "The @code block of the new component, verbatim, declaring its [Parameter]s.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "tag": {
      "description": "Markup that replaces the block in the source file; a self-closing tag of the new component when omitted.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "newFile": {
      "description": "Absolute path for the new file; a sibling named after the component when omitted.",
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
    "startLine",
    "endLine",
    "name"
  ]
}
```

</details>

## <code>rename_<wbr>symbol</code>

Plan a solution-wide rename and return the edits for you to apply. This tool never writes to disk. The compiler decides what a reference is, so overloads, overrides, interface implementations, nameof and documentation references are all covered without text search. Two parts of the answer matter equally. 'edits' is what the compiler guarantees. 'unhandled' lists text it will not touch and you must judge: string literals, comments, container registrations by name, unrelated symbols sharing the name. 'conflicts' lists compiler errors the rename would introduce; a non-empty list means do not apply it. A Blazor component is planned too: its .razor file rename and its markup tags arrive as edits, with the rest of the markup under 'unhandled'. An @code member such as a [Parameter] renames as well: attribute assignments in consumer markup are edited at the exact positions the compiler maps back. If the name matches several symbols the answer carries 'candidates' and no edits: pick one and call again by id.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `newName` | `string` | yes | — | New name for the symbol. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare symbol name; used only when it matches exactly one symbol, otherwise candidates come back. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `renameOverloads` | `boolean` | — | `true` | Rename every overload of the method, not just this one. |
| `renameFile` | `boolean` | — | `true` | Rename the file when its name matches the type. |
| `searchComments` | `boolean` | — | `false` | Also rewrite occurrences inside comments. |
| `searchStrings` | `boolean` | — | `false` | Also rewrite occurrences inside string literals. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "newName": {
      "description": "New name for the symbol.",
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
      "description": "Bare symbol name; used only when it matches exactly one symbol, otherwise candidates come back.",
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
    "renameOverloads": {
      "description": "Rename every overload of the method, not just this one.",
      "type": "boolean",
      "default": true
    },
    "renameFile": {
      "description": "Rename the file when its name matches the type.",
      "type": "boolean",
      "default": true
    },
    "searchComments": {
      "description": "Also rewrite occurrences inside comments.",
      "type": "boolean",
      "default": false
    },
    "searchStrings": {
      "description": "Also rewrite occurrences inside string literals.",
      "type": "boolean",
      "default": false
    }
  },
  "required": [
    "solutionId",
    "newName"
  ]
}
```

</details>

## <code>rename_<wbr>namespace</code>

Plan a namespace rename across the solution and return the edits for you to apply. This tool never writes to disk. With updateFolders the file moves that realign the folders with the new namespace are included. Old and new must have the same number of segments; otherwise no edits are proposed and the textual picture is returned instead.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `oldNamespace` | `string` | yes | — | Namespace as it is written today, fully qualified. |
| `newNamespace` | `string` | yes | — | Namespace it should read as, fully qualified and with the same number of segments. |
| `updateFolders` | `boolean` | — | `true` | Also move the files so the folders match the new namespace. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "oldNamespace": {
      "description": "Namespace as it is written today, fully qualified.",
      "type": "string"
    },
    "newNamespace": {
      "description": "Namespace it should read as, fully qualified and with the same number of segments.",
      "type": "string"
    },
    "updateFolders": {
      "description": "Also move the files so the folders match the new namespace.",
      "type": "boolean",
      "default": true
    }
  },
  "required": [
    "solutionId",
    "oldNamespace",
    "newNamespace"
  ]
}
```

</details>

## <code>move_<wbr>type</code>

Plan moving a type to another file, another namespace, or both, and return the edits for you to apply. This tool never writes to disk. The type keeps its name, so references stay valid unless the namespace changed; 'conflicts' lists any compiler error the move would introduce.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare type name; used only when it matches exactly one type. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `targetFile` | `string?` | — | `null` | Absolute path of the file the type should live in; a sibling file named after the type when omitted. |
| `targetNamespace` | `string?` | — | `null` | Namespace the type should declare; unchanged when omitted. |

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
      "description": "Bare type name; used only when it matches exactly one type.",
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
    "targetFile": {
      "description": "Absolute path of the file the type should live in; a sibling file named after the type when omitted.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "targetNamespace": {
      "description": "Namespace the type should declare; unchanged when omitted.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>organize_<wbr>usings</code>

Plan the removal of using directives the compiler reports as unnecessary and sort the rest, System first. This tool never writes to disk. Knowing which directive is unnecessary needs semantics, which is why this is not a text edit.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `scope` | `string?` | — | `null` | Optional absolute file or directory path that narrows the work. |

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
      "description": "Optional absolute file or directory path that narrows the work.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>change_<wbr>signature</code>

Plan a change to a method signature and return the edit for the declaration. This tool never writes to disk. The compiler offers no cascading signature change, so every call site that stops compiling is listed under 'conflicts' — that list is complete and is your work list.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare method name; used only when it matches exactly one method. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `parameters` | `array?` | — | `null` | Parameters the method should take, each written as 'Type name'; unchanged when omitted. |
| `returnType` | `string?` | — | `null` | Return type the method should have; unchanged when omitted. |

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
      "description": "Bare method name; used only when it matches exactly one method.",
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
    "parameters": {
      "description": "Parameters the method should take, each written as \u0027Type name\u0027; unchanged when omitted.",
      "type": [
        "array",
        "null"
      ],
      "items": {
        "type": [
          "string",
          "null"
        ]
      },
      "default": null
    },
    "returnType": {
      "description": "Return type the method should have; unchanged when omitted.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

