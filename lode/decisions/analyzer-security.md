# Analyzer Decisions: Security
*Updated: 2026-11-21T21:17:18Z*

Rationale removed from source comments. Each section names the type that carried it.
Read this page before you change or delete the matching check.

## FileSystemPathAnalyzer
- The FileSystemPathAnalyzer format string maps {0} to the parameter name, {1} to the description, and {2} to the suggested type. In the name-only case {2} is "FileInfo' or 'DirectoryInfo". As a result, the format string's own single quotes complete the message.
- Applies the name pattern only when no body exists (interface, abstract method, empty stub, record primary constructor). A non-empty body uses the body scan. As a result, a display-only method with a path-like parameter name stays silent.
- Scans a method body once and reuses that scan for every string parameter. As a result, the cost stays linear in the number of parameters.
- Follows the local derivation map to a fixed point. A chain of `Path.*` calls or identifier copies therefore reaches the parameter at any depth.
- Cannot inspect a primary constructor body. As a result, only the name pattern applies to its parameters.

## FileSystemPathHelpers
- Reads SeparatedSyntaxList.Count instead of calling Any() because Count is O(1) and Any() allocates an enumerator (RCS9004).

## IoMethodCatalog
- Treats Path.* calls and identifier copies as derivation edges. A local inherits path-ness through any number of hops and fires when later passed to File.* or Directory.*

## PathNamePatterns
- Checks exclusions before positive patterns, and a match in the exclusion list suppresses every positive pattern.
- Omits "file" from the positive patterns on purpose: a filePath parameter fires through "path", while fileName must stay silent.
- Adds input, output, and file for CLI options because a --file option is almost always a file path, while a fileName parameter stays silent.

## FipsCipherModeAnalyzer
- Maps the CipherMode values: CBC is 1, ECB is 2, OFB is 3, CFB is 4, and CTS is 5.

## FipsDirectInstantiationAnalyzer
- Offers no factory advice for the types E128071 already blocks. This holds because the two rules would contradict each other.
- Accepts only a symmetric algorithm for Rijndael outside System.Security.Cryptography because the name is ambiguous and Rijndael allows a block size that AES does not.

## FipsForbiddenAlgorithmNameAnalyzer
- Covers only the name-only cipher matches. E128071 owns the policy's named algorithms.

## FipsHmacKeyLengthAnalyzer
- Excludes Create because it returns a new HMAC instance rather than hashing the key.
- Takes the concrete type from the initializer of a base-typed local such as HMAC h = new HMACSHA256().

## FipsPbkdfIterationAnalyzer
- Analyzes and reports generated code because a generated PBKDF2 call is a real policy defect.
- Checks only a named iterations or hashAlgorithm argument on a subclass constructor. This holds because the subclass picks its own parameters. The overload defaults apply to Rfc2898DeriveBytes itself.
- Maps KeyDerivationPrf values: HMACSHA1 is 0, HMACSHA256 is 1, and HMACSHA512 is 2.

## FipsRsaKeySizeAnalyzer
- Analyzes and reports generated code because a generated key setup is a real policy defect.
- Treats RSA.Create() on .NET Framework as RSACryptoServiceProvider because mscorlib defines object there.
- Reports a curve-less ECDsa factory because it creates the default P-256 key, which is below the policy minimum.
- Reports a key factory with no curve and no key size. The type then creates the default P-256 key, which is below the policy minimum.
- Inspects ECParameters.Curve through the field because a parameters struct can carry a weak curve.
- Does not treat a KeySize assignment as a replacement for the default report. This holds because assigning KeySize does not reliably resize an RSACryptoServiceProvider key. Only an import replaces it, and the import reports for itself.
- Reports an unresolvable key-size value on purpose. As a result, an unreadable curve cannot hide an unapproved curve.
- Resolves a receiver through its single initializer. An upcast local and a name factory therefore reach the RSA KeySize and import checks.
- Resolves a name factory by its constant algorithm name. A local that holds `AsymmetricAlgorithm.Create("RSA")` counts as an RSA key.

## FipsSymbolHelpers.Assignments
- Walks each body once to collect every written local. As a result, each later reference is a set lookup.

## FipsSymbolHelpers.BufferLength
- Caps the source-derived text it decodes or scans so a hostile literal cannot inflate analyzer memory and time. An over-cap value falls through to the fail-closed report.
- FipsSymbolHelpers reads stackalloc through syntax because Roslyn exposes no public operation interface for it.

## FipsSymbolHelpers
- Does not report a collection or dictionary probe that reads a hash name. This holds because the name guards against an algorithm rather than selecting it.
- Keys its cache on the root operation of a body. As a result, the entry dies with the operation tree.
- Skips a compound assignment because it computes a new value from the old one. The result is not a known constant and the right operand alone is not the assigned value.
- Reads the receiver of Equals or HasFlag as the value, and treats only the argument position as a possible misleading name.

## FipsTlsVersionAnalyzer
- Maps the SslProtocols and SecurityProtocolType bits below TLS 1.2: Ssl2 is 12, Ssl3 is 48, Tls is 192, and Tls11 is 768.
- Resolves the protocol by parameter because named arguments can reorder the call.

## FipsTypeClassifier
- Skips the string scans for a framework type outside System.Security.Cryptography because such a type is never a forbidden algorithm.

## FipsUnapprovedHashAnalyzer
- Reports RSAOAEPKeyExchangeFormatter and its deformatter because they always use OAEP with SHA-1.
- Reports RSAPKCS1KeyExchangeFormatter and its deformatter because they always use PKCS#1 v1.5 padding.
- Leaves the PBKDF2 and PBE hash arguments to E128145.
- Reports one diagnostic per call. As a result, a SHA-1 hash name on the same call would repeat it.
- Allows verification of an existing signature and leaves PBKDF2 to E128145.
- Maps RSACryptoServiceProvider.Encrypt(data, fOAEP): true selects OAEP with SHA-1, false selects PKCS#1 v1.5, and a flag that is not constant can select either.
- Reports HashAlgorithm.Create() and its peers because they select SHA-1 or HMAC-SHA1 by default.

## HardcodedSecretAnalyzer
- Treats a name that ends in a lookup, header, or label word as a non-credential. This holds because the value is not the secret.
- Treats an indexer key such as ["Password"] as naming the credential.
- Counts a return statement only when it sits in the getter of a property.
- Treats a header name or config path as a setting label, not a secret. It looks for a secret that mixes digits and symbols or uses lowercase words.
- Treats an uppercase name with an underscore as naming the setting a credential is read from. The underscore requirement keeps a plain uppercase secret such as LETMEIN in scope.
- Parses a connection string as key=value pairs separated by semicolons. As a result, a URL query string such as "?host=a&password=b" does not match.

## HardcodedUrlAnalyzer
- Keeps namespace and schema identifiers silent because they are names, not endpoints.
- Excludes a property initializer on an options class and a const or static readonly field. This holds because each declares one fixed value in one place rather than the per-environment endpoint the rule targets. A local const stays reported.
- Matches the host itself or a subdomain. This holds because a bare suffix match would allow evilexample.com.

## HttpBodyReadWithoutSizeBoundAnalyzer
- Requires the `HttpContent` type before the rule runs. Without that type there is no report to make, and a name-only match would flag an unrelated member.

## HttpClientCertificateBypassAnalyzer
- Accepts DangerousAcceptAnyServerCertificateValidator by name alone because the framework property has no other meaning.
- Follows a chain of up to five local, field, or method sources. A callback held in a helper local therefore still resolves to its body.
- Treats a block as unconditional true when every return statement is always true. A block with a false return stays silent.

## InsecureRandomInCryptoContextAnalyzer
- Matches a fragment that collides with an ordinary word only as a whole word in camelCase or snake_case. A bare "session" is excluded because sessionCount and sessionTimeout are not secrets, while sessionId and session_id are.
- Reads and reports generated code because a generated file can create Random for a security-sensitive value.
- Never caches a partial answer because a canceled scan throws before it stores a result.
- Lets a member reached through a value from another file mark the file, even when the file never spells out the type.

## PfxImportEphemeralKeySetAnalyzer
- Stays off when either type is absent. This holds because it has no symbol to match and will not fall back to a name.

## RecursiveDeleteRootAnalyzer
- Requires both System.IO types before it runs. When either type is absent, the members belong to some other type.
- Applies a syntactic gate before overload resolution because most invocations are not a delete.
- Treats the recursive flag as the second parameter of Directory.Delete(string, bool) and Directory.Delete(string, bool, bool).
- Reports a filesystem root because it holds files that other programs own.
- Accepts only a relative literal as a fixed root. This holds because a rooted literal names an absolute location the rule cannot verify.
- Takes Path.Combine's safety from its first argument.
- Rejects a culture-sensitive comparison and a bare prefix as containment proof because both prove too little.
- Treats a write to the checked value between the guard and the delete as invalidating the guard.
- Treats a guard that encloses the delete as proving the path only in its then branch. This holds because an else branch runs when the test failed.
- Accepts only the statement directly before the delete as its guard. This holds because an earlier if is too far away to prove the value at the delete.
- Relies on Path.GetTempPath returning a path that ends with a directory separator.
- Proves the path from the end of an enclosing guard's condition and from the end of a preceding guard's statement.
- Does not recognize a run-time-computed value as a root. As a result, a readonly field and a get-only property do not qualify. A local does qualify when its single initializer is a safe root.

## SensitiveLogArgumentAnalyzer
- Matches a short word only as a whole word. This holds because a substring match would report processNumber through "ssn" inside "process".
- Treats a name with a metadata ending, or the word cancellation, as metadata rather than the sensitive value.
- Treats only the holes of an interpolated template as values. A LoggerMessage method has no template parameter. As a result, every string is a value.
- Reports a call argument at the argument and an array-initializer element at the element.
- Matches the fully qualified format, which leaves out the nullable annotation, so Exception? still matches.

## TimingSafeComparisonAnalyzer
- Treats a weak word as a secret signal only when both operands carry a weak word.
- Leaves span equality alone because it compares the address and the length in constant time. Only the string forms leak a matching prefix.
- Requires two weak names for a secret signal. This holds because a content hash compare is not a forgery check.
- Inspects only a declarator in the current syntax tree because RS1030 forbids Compilation.GetSemanticModel inside an analyzer. A field initialized in another partial file is a known false negative.
- Walks the receiver type and its base types to find HMAC.ComputeHash. This holds because the declaring type alone misses the HashAlgorithm declaration.

## UncontainedRootJoinAnalyzer
- Reads the literal parts of the format string because they carry the traversal. $"../{index}.txt" has one numeric hole and still climbs.

## UnvalidatedJwtIdentityAnalyzer
- Uses a conditional branch as the guard scope. As a result, the branch's own syntax locates the use.
- Treats a local function as able to run before its declaration, and a lambda as able to run after later writes.
- Reports an identity add that follows the sink. A reference holds the identity, so the claim still reaches the request.
- Counts only an add before the sink for a copied identity. This holds because the identity copies its claim collection when it is built.
- Orders a for incrementor by source position because the walker visits it after the body.
- Treats a definition after the anchor in a shared loop as reaching the anchor on the next iteration.
- Counts a parent validation before a lambda as covering it. This holds because a lambda runs after its creation. A local function can run before its declaration. As a result, only a parent guard that scopes it counts.

## Blittability
- Checks struct fields because IsUnmanagedType accepts a struct that holds a bool or decimal.

## FixedBufferFieldAnalyzer
- Requires C# 12 and a runtime that defines InlineArrayAttribute (.NET 8) before it reports.
- Reports a declaration with several buffers once. Only the code fix needs a single declarator.

## FixedStringMutationAnalyzer
- Treats a read through a pinned string as normal interop. Only a write through the pointer breaks string immutability, and a copy of the pointer into another variable is not followed.

## FunctionPointerDelegateLifetimeAnalyzer
- Treats a temporary delegate as unowned. As a result, nothing keeps it alive after the call.

## NonPublicBclReflectionAnalyzer
- Requires the framework public key token. This holds because a name prefix also selects a user assembly named System.Foo. An unsigned assembly has no token.

## NullByRefAnalyzer
- Accepts a ref return of Unsafe.NullRef paired with Unsafe.IsNullRef because that is the documented "not found" idiom used by CollectionsMarshal.GetValueRefOrNullRef.

## UnsafeAddArrayDataReferenceAnalyzer
- Matches only the generic T[] overload. This holds because the Array overload returns ref byte and uses byte offsets.

## UnsafeAsPointerEscapeAnalyzer
- Accepts a pointer to a plain local or by-value parameter because the stack never moves.
- Rejects a pointer from an async member because the state machine lives on the heap and moves during a collection.

## UnsafeAsToBitCastAnalyzer
- Stays silent when Unsafe.BitCast is absent. This holds because earlier targets have no replacement to offer.

Related: [Decision Records](summary.md) explains how to use this page.
