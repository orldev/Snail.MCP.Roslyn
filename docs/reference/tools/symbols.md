# Symbols, references and hierarchies

## <code>search_<wbr>symbols</code>

Find symbols whose name contains the query and return the identifier every other tool expects. This is the entry point: resolve a name to an identifier here, then pass that identifier on. Never guess an identifier and never rename by bare name when more than one symbol matches.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `query` | `string` | yes | — | Part of the symbol name; matching ignores case. |
| `kind` | `Any` \| `Type` \| `Method` \| `Property` \| `Field` \| `Event` | — | `Any` | Narrow to one declaration family: Any, Type, Method, Property, Field or Event. |
| `limit` | `integer` | — | `30` | Maximum candidates to return. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "query": {
      "description": "Part of the symbol name; matching ignores case.",
      "type": "string"
    },
    "kind": {
      "description": "Narrow to one declaration family: Any, Type, Method, Property, Field or Event.",
      "type": "string",
      "enum": [
        "Any",
        "Type",
        "Method",
        "Property",
        "Field",
        "Event"
      ],
      "default": "Any"
    },
    "limit": {
      "description": "Maximum candidates to return.",
      "type": "integer",
      "default": 30
    }
  },
  "required": [
    "solutionId",
    "query"
  ]
}
```

</details>

## <code>get_<wbr>symbol_<wbr>info</code>

Describe one symbol: kind, signature, accessibility, namespace, declaration site and documentation. Address it either by the identifier from search_symbols or by a file position.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |

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
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>get_<wbr>symbol_<wbr>source</code>

Return the source text that declares one symbol, cut to its own span, so you can read a type or a method without knowing which file holds it or how many lines it spans. Address it by the identifier from search_symbols or by a file position. Prefer this over reading a whole file when you only need one declaration.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |

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
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>find_<wbr>references</code>

List every place the compiler treats as a use of this symbol, so you can see what breaks before you touch it. Covers overrides, interface implementations, nameof and documentation references, not text matches. Each reference carries a kind — read, write, invocation, object_creation, type_argument and so on — so fifty results can be judged without reading fifty files. A use inside a .razor or .cshtml file is returned with kind 'markup' and the authored file, without a line. Scope is the loaded solution: uses from projects outside it are invisible here and are not reported as absent.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `limit` | `integer` | — | `100` | Maximum references to return. |

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
      "description": "Maximum references to return.",
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

## <code>find_<wbr>implementations</code>

List what implements or derives from this symbol: implementations of an interface or interface member, derived classes, derived interfaces, overriding members. These are compiler edges, invisible to text search, and they are what a signature change breaks.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare name; used only when it matches exactly one symbol. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `limit` | `integer` | — | `50` | Maximum results to return. |

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
      "description": "Maximum results to return.",
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

## <code>find_<wbr>callers</code>

List the methods that call this one, as symbols rather than as locations, so a call graph can be walked upwards without reading files.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare name; used only when it matches exactly one symbol. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |
| `limit` | `integer` | — | `50` | Maximum results to return. |

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
      "description": "Maximum results to return.",
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

## <code>get_<wbr>type_<wbr>hierarchy</code>

Show where a type sits: what it inherits, every interface it carries, and everything that derives from it. One call replaces a chain of find_references.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `symbolId` | `string?` | — | `null` | Symbol id from search_symbols. |
| `name` | `string?` | — | `null` | Bare type name; used only when it matches exactly one type. |
| `file` | `string?` | — | `null` | Absolute file path. |
| `line` | `integer` | — | `0` | 1-based line. |
| `column` | `integer` | — | `0` | 1-based column. |

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
    }
  },
  "required": [
    "solutionId"
  ]
}
```

</details>

## <code>get_<wbr>generated_<wbr>code</code>

List what the source generators contributed to a project, so a symbol with no declaration in source can be traced. Generated files are rebuilt on every compile: editing them changes nothing, so change the input the generator reads instead.

*read-only · every call adds · stays on this machine.*

| Parameter | Type | Required | Default | Description |
| --- | --- | --- | --- | --- |
| `solutionId` | `string` | yes | — | From load_solution. |
| `project` | `string?` | — | `null` | Project name or part of it; the first C# project when omitted. |
| `hintName` | `string?` | — | `null` | Part of the generated file name, to narrow the list. |
| `includeText` | `boolean` | — | `false` | Include the generated text itself, not just the file list. |
| `limit` | `integer` | — | `20` | Maximum files to return. |

<details><summary>JSON Schema</summary>

```json
{
  "type": "object",
  "properties": {
    "solutionId": {
      "description": "From load_solution.",
      "type": "string"
    },
    "project": {
      "description": "Project name or part of it; the first C# project when omitted.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "hintName": {
      "description": "Part of the generated file name, to narrow the list.",
      "type": [
        "string",
        "null"
      ],
      "default": null
    },
    "includeText": {
      "description": "Include the generated text itself, not just the file list.",
      "type": "boolean",
      "default": false
    },
    "limit": {
      "description": "Maximum files to return.",
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

