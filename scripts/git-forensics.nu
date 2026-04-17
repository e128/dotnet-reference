#!/usr/bin/env nu

# Git forensics — 5 commands for reading unfamiliar codebases.
# Source: piechowski.io/post/git-commands-before-reading-code/
# Portable: works in any git repo.

def main [
    command?: string  # churn | contributors | bugs | velocity | firefights | all
    --since: string = "1 year ago"  # Time window for churn/bugs/firefights
    --top: int = 20                 # Number of results for ranked lists
    --json                          # Output as structured data
] {
    let cmd = ($command | default "all")

    match $cmd {
        "churn" => { churn $since $top $json }
        "contributors" => { contributors $top $json }
        "bugs" => { bugs $since $top $json }
        "velocity" => { velocity $json }
        "firefights" => { firefights $since $json }
        "all" => { run-all $since $top $json }
        _ => {
            print $"Unknown command: ($cmd)"
            print "Available: churn | contributors | bugs | velocity | firefights | all"
        }
    }
}

# Files with the most commits (highest churn)
def churn [since: string, top: int, as_json: bool] {
    let raw = (git log --format=format: --name-only $"--since=($since)"
        | lines
        | where { |l| ($l | str trim) != "" }
        | sort
        | uniq --count
        | sort-by count --reverse
        | rename file count
        | where { |row| ($row.file | path exists) }
        | first $top)

    if $as_json { $raw | to json } else {
        print $"(ansi cyan)Most-changed files \(since ($since)):(ansi reset)"
        print ($raw | table)
    }
}

# Contributors ranked by commit count
def contributors [top: int, as_json: bool] {
    let raw = (git shortlog -sn --no-merges --all
        | lines
        | where { |l| ($l | str trim) != "" }
        | each { |l|
            let trimmed = ($l | str trim)
            let idx = ($trimmed | str index-of "\t" | default ($trimmed | str index-of " "))
            let count_str = ($trimmed | str substring 0..$idx | str trim)
            let author = ($trimmed | str substring ($idx + 1).. | str trim)
            { count: ($count_str | into int), author: $author }
        }
        | first $top)

    if $as_json { $raw | to json } else {
        print $"(ansi cyan)Top contributors:(ansi reset)"
        print ($raw | table)
    }
}

# Files most associated with bug-fix commits
def bugs [since: string, top: int, as_json: bool] {
    let raw = (git log -i -E $"--grep=fix|bug|broken" --name-only --format='' $"--since=($since)"
        | lines
        | where { |l| ($l | str trim) != "" }
        | sort
        | uniq --count
        | sort-by count --reverse
        | rename file count
        | where { |row| ($row.file | path exists) }
        | first $top)

    if $as_json { $raw | to json } else {
        print $"(ansi cyan)Bug hotspots \(since ($since)):(ansi reset)"
        print ($raw | table)
    }
}

# Commits per month across the repo lifetime
def velocity [as_json: bool] {
    let raw = (git log --format='%ad' --date=format:'%Y-%m'
        | lines
        | where { |l| ($l | str trim) != "" }
        | sort
        | uniq --count
        | rename month count)

    if $as_json { $raw | to json } else {
        print $"(ansi cyan)Commit velocity by month:(ansi reset)"
        print ($raw | table)
    }
}

# Reverts, hotfixes, and emergency commits
def firefights [since: string, as_json: bool] {
    let raw = (git log --oneline $"--since=($since)"
        | lines
        | where { |l| $l =~ '(?i)(revert|hotfix|emergency|rollback)' }
        | each { |l|
            let parts = ($l | split row " " --number 2)
            { hash: $parts.0, message: ($parts.1? | default "") }
        })

    if $as_json { $raw | to json } else {
        let count = ($raw | length)
        print $"(ansi cyan)Firefighting commits \(since ($since)): ($count)(ansi reset)"
        if $count > 0 { print ($raw | table) } else { print "None found." }
    }
}

# Run all 5 analyses
def run-all [since: string, top: int, as_json: bool] {
    if $as_json {
        {
            churn: (churn $since $top true | from json)
            contributors: (contributors $top true | from json)
            bugs: (bugs $since $top true | from json)
            velocity: (velocity true | from json)
            firefights: (firefights $since true | from json)
        } | to json
    } else {
        churn $since $top false
        print ""
        contributors $top false
        print ""
        bugs $since $top false
        print ""
        velocity false
        print ""
        firefights $since false
    }
}
