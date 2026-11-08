# Tools

32 tools in 8 families.
The names, descriptions and schemas are generated from the server itself, so they are exactly what the model receives in `tools/list`.

An MCP server that keeps a C# solution compiled in memory and answers an agent faster than it loses the thread. The compiler, not text search, decides what a reference is, and the server never writes to your code: every refactoring returns edits for you to apply.

Call `load_solution` once, then pass the returned `solutionId` to everything else.

| Tool | Family | What it does |
| --- | --- | --- |
| [`load_solution`](solution.md#load_solution) | The solution: load, diagnostics, files, overview | Load a C# solution or project and keep it compiled for later questions. |
| [`get_diagnostics`](solution.md#get_diagnostics) | The solution: load, diagnostics, files, overview | List compiler errors and warnings for a loaded solution, reflecting the files as they are on disk right now. |
| [`read_file`](solution.md#read_file) | The solution: load, diagnostics, files, overview | Read one file the way the compiler sees it: the numbered text, the diagnostics that live in it, and every declaration it holds with the identifier the symbol… |
| [`get_solution_overview`](solution.md#get_solution_overview) | The solution: load, diagnostics, files, overview | Describe the whole solution in one budgeted answer: project graph, sizes, compiler errors, the types the rest of the code names most often, and the structura… |
| [`check_architecture`](solution.md#check_architecture) | The solution: load, diagnostics, files, overview | List structural breaches: namespaces that do not match their folder, namespaces that depend on each other in a cycle, projects that do, and markup blocks the… |
| [`health_check`](solution.md#health_check) | The solution: load, diagnostics, files, overview | Report whether the server itself is in a state where its answers can be trusted: readiness, project and document counts, how long a full diagnostic pass take… |
| [`unload_solution`](solution.md#unload_solution) | The solution: load, diagnostics, files, overview | Release a loaded solution and the memory its compilations hold. |
| [`search_symbols`](symbols.md#search_symbols) | Symbols, references and hierarchies | Find symbols whose name contains the query and return the identifier every other tool expects. |
| [`get_symbol_info`](symbols.md#get_symbol_info) | Symbols, references and hierarchies | Describe one symbol: kind, signature, accessibility, namespace, declaration site and documentation. |
| [`get_symbol_source`](symbols.md#get_symbol_source) | Symbols, references and hierarchies | Return the source text that declares one symbol, cut to its own span, so you can read a type or a method without knowing which file holds it or how many line… |
| [`find_references`](symbols.md#find_references) | Symbols, references and hierarchies | List every place the compiler treats as a use of this symbol, so you can see what breaks before you touch it. |
| [`find_implementations`](symbols.md#find_implementations) | Symbols, references and hierarchies | List what implements or derives from this symbol: implementations of an interface or interface member, derived classes, derived interfaces, overriding members. |
| [`find_callers`](symbols.md#find_callers) | Symbols, references and hierarchies | List the methods that call this one, as symbols rather than as locations, so a call graph can be walked upwards without reading files. |
| [`get_type_hierarchy`](symbols.md#get_type_hierarchy) | Symbols, references and hierarchies | Show where a type sits: what it inherits, every interface it carries, and everything that derives from it. |
| [`get_generated_code`](symbols.md#get_generated_code) | Symbols, references and hierarchies | List what the source generators contributed to a project, so a symbol with no declaration in source can be traced. |
| [`extract_component`](refactor.md#extract_component) | Refactoring plans | Plan the extraction of a markup block from a .razor file into a new component. |
| [`rename_symbol`](refactor.md#rename_symbol) | Refactoring plans | Plan a solution-wide rename and return the edits for you to apply. |
| [`rename_namespace`](refactor.md#rename_namespace) | Refactoring plans | Plan a namespace rename across the solution and return the edits for you to apply. |
| [`move_type`](refactor.md#move_type) | Refactoring plans | Plan moving a type to another file, another namespace, or both, and return the edits for you to apply. |
| [`organize_usings`](refactor.md#organize_usings) | Refactoring plans | Plan the removal of using directives the compiler reports as unnecessary and sort the rest, System first. |
| [`change_signature`](refactor.md#change_signature) | Refactoring plans | Plan a change to a method signature and return the edit for the declaration. |
| [`analyze_change_impact`](analysis.md#analyze_change_impact) | Impact, analyzers, fixes and validation | Measure what depends on a symbol before anyone changes it: references grouped by project, the overrides and interface implementations a shape change would br… |
| [`run_analyzers`](analysis.md#run_analyzers) | Impact, analyzers, fixes and validation | Run the analyzers over the solution and report what they find, with the category that explains why it matters. |
| [`get_code_fixes`](analysis.md#get_code_fixes) | Impact, analyzers, fixes and validation | Return the repairs the compiler's own providers offer for a diagnostic, as edits for you to apply. |
| [`validate`](analysis.md#validate) | Impact, analyzers, fixes and validation | Compile a file's proposed contents in the context of the solution, before writing anything to disk. |
| [`get_di_registrations`](architecture.md#get_di_registrations) | Registrations, unused and untested code | List the dependency injection registrations found in source: service, implementation and lifetime, with the line that declares them. |
| [`find_unused_symbols`](architecture.md#find_unused_symbols) | Registrations, unused and untested code | List declarations that no code in the loaded solution references. |
| [`find_uncovered_symbols`](architecture.md#find_uncovered_symbols) | Registrations, unused and untested code | List public declarations that no test project in this solution references. |
| [`find_tests_for_symbol`](architecture.md#find_tests_for_symbol) | Registrations, unused and untested code | List the test methods that reach this symbol, so you know what will catch a change to it before you make one. |
| [`run_script`](script.md#run_script) | Scripts over the whole solution | Evaluate a C# script against the loaded solution and return its value and printed output. |
| [`run_tools`](sequence.md#run_tools) | Several tools in one call | Runs several of this server's tools in one call, in order, stopping at the first that fails. |
| [`find_tool`](discovery.md#find_tool) | Finding a tool | Finds this server's tools for a task described in words, with what each takes, enough to call it at once. |
