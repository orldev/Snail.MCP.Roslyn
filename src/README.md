# Snail.MCP.Roslyn

An MCP server that keeps a C# solution compiled in memory and answers an agent faster than it loses the thread.

The compiler, not text search, decides what a reference is. That is the whole point: an agent can grep, but it
cannot ask a question of the entire solution, and it cannot know in milliseconds what its last edit broke.

## Principles

- **The server never writes to your code.** Every refactoring returns edits for you to apply, and says so in its description.
- **Freshness is guaranteed, not flagged.** Every answer reflects the files as they are on disk at the moment of the call,
  including files added since the solution was loaded.
- **An empty answer must explain itself.** "Searched and found nothing" and "there was nowhere to search" are different
  results, and only one of them is safe to act on.
- **Every answer is budgeted, and truncation is always stated.** Analysis always covers the whole solution; only the
  answer is cut to size.

## Tools

Call `load_solution` once, then pass the returned `solutionId` to everything else.

**Solution**
`load_solution` · `unload_solution` · `get_diagnostics` · `get_solution_overview` · `check_architecture` · `health_check`

**Symbols**
`search_symbols` · `get_symbol_info` · `get_symbol_source` · `find_references` · `find_implementations` ·
`find_callers` · `get_type_hierarchy` · `get_generated_code`

**Refactoring — returns edits, never writes**
`rename_symbol` · `rename_namespace` · `move_type` · `organize_usings` · `change_signature`

**Before and after a change**
`analyze_change_impact` · `validate` · `run_analyzers` · `get_code_fixes`

**Wiring and coverage**
`get_di_registrations` · `find_unused_symbols` · `find_uncovered_symbols` · `find_tests_for_symbol`

**Escape hatch**
`run_script` — a C# script against the warm solution, for the question no fixed tool anticipates.

### References carry a verdict, not just a place

Every reference from `find_references` is classified — `read`, `write`, `invocation`, `object_creation`,
`type_argument`, `nameof`, `attribute` and so on — so fifty results can be judged without reading fifty files.
A use inside a `.razor`/`.cshtml` file is returned with kind `markup` and the authored file: a component tag has
no line number in the sources, and none is invented.

### The two halves of a rename

`rename_symbol` returns `edits` — what the compiler guarantees — and `unhandled` — text it will not touch and you must
judge: string literals, comments, container registrations by name, unrelated symbols sharing the name. A tool that
silently renamed half of them would be the original problem in new packaging.

## Тёплое решение

`load_solution` на решении, которое уже загружено, **не открывает его заново**: берётся то, что в памяти,
и приводится в соответствие с диском — так же, как это делает всякий другой вызов. Изменённые файлы
перечитываются, а если проекты добавились или исчезли, кэш переоткрывает решение сам.

Замер на решении платформы, 14 проектов, в контейнере:

| | время |
|---|---|
| первая загрузка | 5043 мс |
| повторные | 70 мс, 59 мс |

Ради этого сервер и переехал из образа воркера в контейнер: процесс, живший один шаг, платил пять секунд
каждый раз. `unload_solution` заставляет открыть заново, когда это зачем-то нужно.

Пакеты сервер восстанавливает сам: рабочую копию задачи клонирует воркер и никогда не собирает, поэтому
`project.assets.json` в ней нет, а без него всякий семантический ответ занижен. Раньше сервер только писал
об этом в примечании — модели, которая сделать с ним ничего не могла. `Workspace:RestoreOnLoad` выключает
это для развёртывания, которое восстанавливает пакеты иначе; `Workspace:RestoreSeconds` (300) — предел,
после которого загрузка идёт дальше без пакетов и честно об этом говорит.

## Requirements

The .NET SDK must be installed: the server loads MSBuild from it through `MSBuildLocator`, so it is deliberately
**not** self-contained. Run `dotnet restore` on the analysed solution first — without it, `load_solution` reports
`Degraded` and semantic answers under-report.

## Configuration

```json
{
  "Workspace": {
    "Port": 8817,
    "RestoreOnLoad": true,
    "RestoreSeconds": 300,
    "Roots": { "/caller/path": "/path/inside/this/process" },
    "MaxIssues": 200,
    "MaxFindings": 200,
    "AllowScripts": true,
    "ScriptTimeoutSeconds": 30,
    "AnalyzerPolicy": "default"
  }
}
```

`Port` decides the shape of the process. Unset, the server speaks stdio to whatever started it, belongs
to that one caller and reaches wherever it does — the shape a desktop client installs. Set, it listens on
every interface and serves sessions at `/mcp`, each naming the copy it works in:
`http://mcp-roslyn:8817/mcp?root=/workspaces/task-7`. One server then stands behind every agent on the
machine, and its warm compilations are shared between steps instead of rebuilt for each.

A session opened at a root **this machine does not have** is refused on the spot, with the root named —
`400 this machine has no /workspaces/task-7`. Once servers answer at addresses, the worst thing that can
happen is an agent on one machine reaching the server of another: it would answer, about a solution that
is not the task's copy, and a wrong answer shaped like a right one is far worse than an error.

`Roots` maps the caller's filesystem to this process's, for running the server in a container. Empty means the two coincide.
`AllowScripts` governs `run_script`, which executes C# in this process with this process's rights: the container,
or the absence of one, is the only boundary. `AnalyzerPolicy` decides where analyzer assemblies may be loaded from —
an analyzer is arbitrary code: `strict` allows the package cache and the SDK, `default` adds the solution's own tree,
`all` disables the check.

Portions are adapted from [roslyn-codelens-mcp](https://github.com/MarcelRoozekrans/roslyn-codelens-mcp) (MIT) — see NOTICE.

## Running it

```json
{
  "mcpServers": {
    "roslyn": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["<path>/src/bin/Debug/net10.0/Snail.MCP.Roslyn.dll"]
    }
  }
}
```

Do not use `dotnet run`: it writes build output to standard output, which belongs to the protocol.
After rebuilding the server, reconnect the client — a running process holds the old image.
