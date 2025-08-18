@{
    # Load all default rules; we’ll override specifics in `Rules`.
    ExcludeRules = @()

    # Where your custom rules live (file or folder).
    <#
    CustomRulePath = @(
        './Lint',                         # folder form (recommended)
        './Lint/AvoidNewObjectRule.psm1'  # file form (optional; keep only one if you prefer)
    )
    #>

    Rules        = @{
        # Built-in rule with your traversal customization
        PSReviewUnusedParameter    = @{
            Enable             = $true
            CommandsToTraverse = @('Where-Object') # Example of traversing a specific command
        }

        # Your custom rule
        AvoidNewObjectRule         = @{
            Enable   = $true
            Severity = 'Warning'
        }

        # Align assignment statements
        PSAlignAssignmentStatement = @{
            Enable         = $true
            CheckHashtable = $false
            CheckPipeline  = $true
        }

        # Avoid long lines
        PSAvoidLongLines           = @{
            Enable            = $true
            MaximumLineLength = 120
        }

        # Avoid using cmdlet aliases
        PSAvoidUsingCmdletAliases  = @{
            AllowList = @('ScriptBlock')
        }

        # Place open brace on new line
        PSPlaceOpenBrace           = @{
            Enable             = $true
            OnSameLine         = $true
            NewLineAfter       = $true
            IgnoreOneLineBlock = $true
        }
        # Place close brace on new line
        PSPlaceCloseBrace          = @{
            Enable             = $true
            NoEmptyLineBefore  = $true
            IgnoreOneLineBlock = $true
            NewLineAfter       = $true
        }

        # Provide comment-based help
        PSProvideCommentHelp       = @{
            Enable                  = $true
            ExportedOnly            = $false
            BlockComment            = $true
            VSCodeSnippetCorrection = $false
            Placement               = 'before'
        }

        # Indentation
        PSUseConsistentIndentation = @{
            Enable              = $true
            IndentationSize     = 4
            PipelineIndentation = 'IncreaseIndentationForFirstPipeline'
            Kind                = 'space'
        }

        # Whitespace
        PSUseConsistentWhitespace  = @{
            Enable                                  = $true
            CheckInnerBrace                         = $true
            CheckOpenBrace                          = $true
            CheckOpenParen                          = $true
            CheckOperator                           = $true
            CheckPipe                               = $true
            CheckPipeForRedundantWhitespace         = $true
            CheckSeparator                          = $true
            CheckParameter                          = $true
            IgnoreAssignmentOperatorInsideHashTable = $true
        }

        # Casing
        PSUseCorrectCasing         = @{
            Enable        = $true
            CheckCommands = $true
            CheckKeyword  = $true
            CheckOperator = $true
        }

        # (Optional) a few opinionated “proof it’s working” rules 
        PSAvoidUsingWriteHost      = @{ Enable = $true }
        PSUseApprovedVerbs         = @{ Enable = $true }
    }
}