# Code and Interface References

Rules for code in text, code samples, command syntax, placeholders, UI references and API comments.

## Code in Text

- Use code font for filenames, paths, class and method names, data types, constants, environment variables, CLI utility names, HTTP verbs and status codes, ports, keywords, placeholders and text the reader types.
- Do not use code font for a product name, an organization name, a general domain name, or a URL meant for navigation.
- Do not inflect a code element. Write "the `ADDRESS` constant's value", not "`ADDRESS`'s value". Write "send a `POST` request".
- Omit the class name from a method name unless it is needed. Do not put quotes around code unless they are part of it.
- Write a filename in code font with the word "file". Write a file type by its formal name: "a PNG file".

## Code Samples and Command Syntax

- Put an introductory sentence before each sample. End it with a colon when the sample follows directly.
- Indent per the language's style, usually two spaces. Wrap at 80 characters.
- Show omitted code with a language comment. Do not use `...` in a sample the reader copies.
- Give a copyable example that needs no editing. Keep optional arguments out of it.
- Show output only when it adds value. Introduce it with "The output is similar to the following:".
- Syntax notation: optional `[ARGUMENT]`, mutually exclusive `{FILE_1|FILE_2}`, repeating `...`.

## Placeholders

- Use uppercase with underscores: `API_NAME`. Do not use `api_name` or `apiName`.
- Do not use possessives such as `YOUR_API_NAME`, and do not use `x` or `xxx`.
- Explain a placeholder where it first appears. For several, introduce the list with "Replace the following:" and list them in order of appearance.

## UI Elements

- Focus on the task, not the interface.
- Put UI element names in bold. Copy the label, but write sentence case for an all-caps label.
- Write "Click **OK**", not "the OK button". Use "field", not "box". Do not call a menu a "drop-down".
- Write a menu path with angle brackets: "Select **View > Tools**".
- Use "in" for dialogs, fields, lists, menus and windows. Use "on" for pages, tabs and toolbars.
- Spell out modifier keys: "Press Control+C".

## API Reference Comments

- Describe every class, method, parameter, return value and exception, in present tense.
- Start a method description with a verb: "Gets the...", "Creates a...". Start a boolean getter with "Checks whether...".
- Do not repeat the class name in its first sentence, and do not write "this class will".
- Write a parameter description as a capitalized phrase that ends with a period. Do not put `true` and `false` in code font in a boolean description.
- Put the replacement in the first sentence of a deprecation notice.

Sources: [code in text](https://developers.google.com/style/code-in-text), [code samples](https://developers.google.com/style/code-samples), [command-line syntax](https://developers.google.com/style/code-syntax), [placeholders](https://developers.google.com/style/placeholders), [UI elements](https://developers.google.com/style/ui-elements), [API reference comments](https://developers.google.com/style/api-reference-comments).
