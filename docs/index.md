---
title: WickdBot Docs
---

# WickdBot Docs

WickdBot documentation is generated from C# XML documentation comments and curated Markdown pages.

- [Programmatic Business Flows](business-flows.md)
- [API Reference](api/index.md)

## Local Preview

Run these commands from the repository root:

```text
dotnet tool restore
dotnet docfx metadata docs/docfx.json
dotnet docfx build docs/docfx.json
dotnet docfx serve docs/_site --port 8080
```
