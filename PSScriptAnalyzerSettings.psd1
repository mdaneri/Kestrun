@{
    # Load all default rules; we’ll override specifics in `Rules`.
    ExcludeRules = @()

    # Where your custom rules live (file or folder).
    CustomRulePath = @(
        './Lint',                         # folder form (recommended)
        './Lint/AvoidNewObjectRule.psm1'  # file form (optional; keep only one if you prefer)
    )

    Rules = @{
        # Built-in rule with your traversal customization
        PSReviewUnusedParameter = @{
            Enable              = $true
            CommandsToTraverse  = @('Where-Object','Remove-PodeRoute')
            Severity            = 'Warning'   # optional: make it louder/softer
        }

        # Your custom rule
        AvoidNewObjectRule = @{
            Enable   = $true
            Severity = 'Warning'
        }

        # (Optional) a few opinionated “proof it’s working” rules
        PSUseConsistentIndentation = @{ Enable = $true }
        PSAvoidUsingWriteHost      = @{ Enable = $true }
        PSUseApprovedVerbs         = @{ Enable = $true }
    }
}