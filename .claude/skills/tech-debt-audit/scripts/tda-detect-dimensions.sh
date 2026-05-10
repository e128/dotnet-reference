#!/usr/bin/env bash
# Detect which conditional .NET audit dimensions apply.
# Outputs a summary of detected technologies and relevant findings.
set -euo pipefail

SCOPE="${1:-.}"

echo "=== Conditional Dimension Detection ==="
echo ""

# --- AOT & Trimming ---
AOT_FILES=$(rg "PublishAot|IsAotCompatible" "${SCOPE}" -l 2>/dev/null || true)
if [[ -n "${AOT_FILES}" ]]; then
  echo "## AOT_TRIMMING: ACTIVE"
  echo "Files: ${AOT_FILES}"
  echo "DynamicDependency usage:"
  rg "DynamicDependency" "${SCOPE}" -c 2>/dev/null || echo "  (none found)"
  echo "JsonSerializerContext (source gen):"
  rg "JsonSerializerContext" "${SCOPE}" -c 2>/dev/null || echo "  (none found)"
else
  echo "## AOT_TRIMMING: INACTIVE"
fi
echo ""

# --- Blazor WASM ---
BLAZOR_FILES=$(rg "Microsoft\.NET\.Sdk\.BlazorWebAssembly" "${SCOPE}" -l 2>/dev/null || true)
if [[ -n "${BLAZOR_FILES}" ]]; then
  echo "## BLAZOR_WASM: ACTIVE"
  echo "Projects: ${BLAZOR_FILES}"
  echo "Deprecated properties:"
  rg "BlazorCacheBootResources|BlazorEnableCompression|JsonSerializerIsReflectionEnabledByDefault" "${SCOPE}" -n 2>/dev/null || echo "  (none found)"
  echo "IJSObjectReference (check for IAsyncDisposable):"
  rg "IJSObjectReference" "${SCOPE}" -l 2>/dev/null || echo "  (none found)"
else
  echo "## BLAZOR_WASM: INACTIVE"
fi
echo ""

# --- EF Core / Data ---
EF_FILES=$(rg "Microsoft\.EntityFrameworkCore" "${SCOPE}" -l 2>/dev/null || true)
if [[ -n "${EF_FILES}" ]]; then
  echo "## EF_CORE: ACTIVE"
  echo "Projects: ${EF_FILES}"
  echo "Migration files:"
  fd "Migration" "${SCOPE}" -e cs 2>/dev/null || echo "  (none found)"
  echo "NotImplementedException in migrations:"
  rg "NotImplementedException" "${SCOPE}" -g "*Migration*.cs" -n 2>/dev/null || echo "  (none found)"
  echo "Down() methods:"
  rg "protected override void Down" "${SCOPE}" -g "*Migration*.cs" -n -A 5 2>/dev/null || echo "  (none found)"
else
  echo "## EF_CORE: INACTIVE"
fi
echo ""

# --- Cloud / Container ---
DOCKER_FILES=$(fd Dockerfile "${SCOPE}" 2>/dev/null || true)
if [[ -n "${DOCKER_FILES}" ]]; then
  echo "## CLOUD_CONTAINER: ACTIVE"
  echo "Dockerfiles: ${DOCKER_FILES}"
  echo "Env var access without fallback:"
  rg "Environment\.GetEnvironmentVariable" "${SCOPE}" -g "*.cs" -c 2>/dev/null || echo "  (none found)"
  echo "Windows-specific APIs:"
  rg "Registry\.|GetFolderPath|COM\b" "${SCOPE}" -g "*.cs" -n 2>/dev/null || echo "  (none found)"
  echo "Health checks:"
  rg "IHealthChecksBuilder|AddHealthChecks" "${SCOPE}" -g "*.cs" -c 2>/dev/null || echo "  (none found)"
  echo "Graceful shutdown:"
  rg "IHostApplicationLifetime|ApplicationStopping" "${SCOPE}" -g "*.cs" -c 2>/dev/null || echo "  (none found)"
else
  echo "## CLOUD_CONTAINER: INACTIVE"
fi
echo ""

# --- FIPS Compliance ---
CRYPTO_FILES=$(rg "System\.Security\.Cryptography" "${SCOPE}" -g "*.cs" -l 2>/dev/null || true)
if [[ -n "${CRYPTO_FILES}" ]]; then
  echo "## FIPS_COMPLIANCE: ACTIVE"
  echo "Crypto files: ${CRYPTO_FILES}"
  echo "Non-FIPS algorithms:"
  rg "MD5|SHA1[^0-9]|DES\b|RC2|TripleDES|Rijndael|HMACMD5|HMACRIPEMD160" "${SCOPE}" -g "*.cs" -n 2>/dev/null || echo "  (none found)"
  echo "Obsolete crypto APIs:"
  rg "RNGCryptoServiceProvider|PasswordDeriveBytes|DSACryptoServiceProvider" "${SCOPE}" -g "*.cs" -n 2>/dev/null || echo "  (none found)"
  echo "Weak TLS:"
  rg "SslProtocols\.(Ssl3|Tls\b|Tls11)|SecurityProtocol" "${SCOPE}" -g "*.cs" -n 2>/dev/null || echo "  (none found)"
  echo "ECB mode:"
  rg "CipherMode\.ECB|Mode\s*=\s*CipherMode" "${SCOPE}" -g "*.cs" -n 2>/dev/null || echo "  (none found)"
  echo "System.Random (potential security misuse):"
  rg "new Random\(\)" "${SCOPE}" -g "*.cs" -n 2>/dev/null || echo "  (none found)"
  echo "Hardcoded keys/IVs:"
  rg "new byte\[\].*\{" "${SCOPE}" -g "*.cs" -n -A2 2>/dev/null | rg -i "key|iv|secret|encrypt" || echo "  (none found)"
  echo "Weak PBKDF2:"
  rg "Rfc2898DeriveBytes" "${SCOPE}" -g "*.cs" -n -A3 2>/dev/null || echo "  (none found)"
  echo "FIPS analyzer guardrails in config:"
  rg -i "ca5350|ca5351|ca5358|ca5364|ca5379|ca5384|ca5385|ca5394|ca5397" .globalconfig .editorconfig 2>/dev/null || echo "  (none found)"
else
  echo "## FIPS_COMPLIANCE: INACTIVE"
fi
echo ""

# --- Service Contract / API Drift ---
OPENAPI=$(fd "openapi|swagger" "${SCOPE}" -e json -e yaml -e yml 2>/dev/null || true)
PROTO=$(fd ".proto" "${SCOPE}" 2>/dev/null || true)
PUBAPI=$(rg "PublicApiAnalyzers|Microsoft\.CodeAnalysis\.PublicApiAnalyzers" "${SCOPE}" -l 2>/dev/null || true)
if [[ -n "${OPENAPI}" || -n "${PROTO}" || -n "${PUBAPI}" ]]; then
  echo "## SERVICE_CONTRACT: ACTIVE"
  [[ -n "${OPENAPI}" ]] && echo "OpenAPI specs: ${OPENAPI}"
  [[ -n "${PROTO}" ]] && echo "Proto files: ${PROTO}"
  [[ -n "${PUBAPI}" ]] && echo "PublicApiAnalyzers: ${PUBAPI}"
  echo "Contract test frameworks:"
  rg "Pact|PactNet" tests/ -l 2>/dev/null || echo "  (none found)"
else
  echo "## SERVICE_CONTRACT: INACTIVE"
fi
echo ""

# --- Fitness Function Coverage ---
echo "=== Fitness Function Coverage ==="
echo "ArchTest frameworks:"
rg "NetArchTest|ArchUnitNET|ArchTest" "${SCOPE}" -l 2>/dev/null || echo "  (none found)"
echo "Architecture tests:"
rg "Architecture|LayerViolation|FitnessFunction" tests/ -l 2>/dev/null || echo "  (none found)"

echo ""
echo "=== Analyzer Configuration ==="
echo ".editorconfig files:"
fd ".editorconfig" "${SCOPE}" 2>/dev/null || echo "  (none found)"
echo ".globalconfig files:"
fd ".globalconfig" "${SCOPE}" 2>/dev/null || echo "  (none found)"
