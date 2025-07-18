@{
    Severity            = @('Error', 'Warning', 'Information')
    IncludeDefaultRules = $true

    CustomRulePath      = @(
        './Lint/AvoidNewObjectRule.psm1'
    )

    Rules               = @{
        PSReviewUnusedParameter = @{
            CommandsToTraverse = @(
                'Where-Object',
                'Remove-PodeRoute'
            )
        }
        AvoidNewObjectRule      = @{
            Severity = 'Warning'
        }
    }

    ExcludeRules        = @(
    )

}