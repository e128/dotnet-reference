# Terminal Output

**Ensure Unicode 15.0 and color font rendering support in all terminal output.** When writing code that emits text to the console:
- Use UTF-8 encoding (the .NET default). Never downgrade to ASCII.
- Emoji and multi-byte characters are valid in CLI output.
- Do not assume the terminal lacks color emoji support.
