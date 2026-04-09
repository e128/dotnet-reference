$basePrompt = Get-Content "$PSScriptRoot/../prompts/SystemPrompt.txt" -Raw
$combinedPrompt = $basePrompt

$remainingArgs = @()
$i = 0

while ($i -lt $args.Count) {
    if ($args[$i] -eq '--append-system-prompt' -and ($i + 1) -lt $args.Count) {
        $combinedPrompt = "$combinedPrompt`n`n$($args[$i + 1])"
        $i += 2
    }
    elseif ($args[$i] -like '--append-system-prompt=*') {
        $extraPrompt = $args[$i] -replace '^--append-system-prompt=', ''
        $combinedPrompt = "$combinedPrompt`n`n$extraPrompt"
        $i += 1
    }
    else {
        $remainingArgs += $args[$i]
        $i += 1
    }
}

& claude --append-system-prompt $combinedPrompt @remainingArgs
