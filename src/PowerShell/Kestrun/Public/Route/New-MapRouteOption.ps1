function New-MapRouteOption {
    <#
    .SYNOPSIS
        Creates a new instance of the Kestrun.Hosting.Options.MapRouteOptions class.
    .DESCRIPTION
        This function initializes a new instance of the MapRouteOptions class, which is used to configure
        routing options for Kestrun server routes.
    .PARAMETER Property
        A hashtable containing properties to set on the MapRouteOptions instance. The keys should match
        the property names of the MapRouteOptions class.
    .OUTPUTS
        [Kestrun.Hosting.Options.MapRouteOptions]
        A new instance of the MapRouteOptions class.
    .EXAMPLE
        $options = New-MapRouteOption -Property @{
            Path = "/myroute"
            HttpVerbs = "Get", "Post"
        }
        This example creates a new MapRouteOptions instance with specified path and HTTP verbs.
    .NOTES
        This function is part of the Kestrun.Hosting module and is used to manage route options.
        Maps to MapRouteOptions constructor.
    .LINK
        https://docs.microsoft.com/en-us/dotnet/api/kestrun.hosting.options.maprouteoptions
    #>
    [Diagnostics.CodeAnalysis.SuppressMessageAttribute('PSUseShouldProcessForStateChangingFunctions', '')]
    [KestrunRuntimeApi('Definition')]
    [CmdletBinding()]
    param(
        [Parameter(Mandatory, ValueFromPipeline)]
        [hashtable] $Property
    )

    process {
        # -- discover the writable properties ----------------------------
        $type = [Kestrun.Hosting.Options.MapRouteOptions]
        $writable = @{}
        foreach ($p in $type.GetProperties([System.Reflection.BindingFlags]::Instance -bor `
                    [System.Reflection.BindingFlags]::Public)) {
            if ($p.SetMethod -and $p.SetMethod.IsPublic) {
                $writable[$p.Name.ToLowerInvariant()] = $p
            }
        }

        # -- create the empty record -------------------------------------
        $options = [Activator]::CreateInstance($type)

        foreach ($key in $Property.Keys) {
            $pName = $key.ToString().ToLowerInvariant()

            # --- unknown key? -------------------------------------------
            if (-not $writable.ContainsKey($pName)) {
                throw "Unknown option '$key'. Valid keys are: $($writable.Keys -join ', ')."
            }

            $prop = $writable[$pName]
            $targetT = $prop.PropertyType
            $rawValue = $Property[$key]

            # --- special case: HttpVerbs can accept strings or enum ------ 
            # ---------- special: HttpVerbs accepts strings or enum ---------
            if ($prop.Name -eq 'HttpVerbs') {
                $converted = @()

                foreach ($v in @($rawValue)) {
                    if ($v -is [Kestrun.Utilities.HttpVerb]) {
                        $converted += $v
                        continue
                    }
                    [Kestrun.Utilities.HttpVerb] $tmpVerb = [Kestrun.Utilities.HttpVerb]::Get
                    if ([Kestrun.Utilities.HttpVerbExtensions]::TryFromMethodString($v, [ref]$tmpVerb)) {
                        $converted += $tmpVerb
                    }
                    else {
                        $valid = [string]::Join(', ', [Enum]::GetNames([Kestrun.Utilities.HttpVerb]))
                        throw "Invalid HTTP verb '$v' in '$key'. Allowed values: $valid."
                    }
                }

                $prop.SetValue($options, [Kestrun.Utilities.HttpVerb[]]$converted, $null)   
                continue
            }

            # --- normal conversion --------------------------------------
            try {
                $converted = [System.Management.Automation.LanguagePrimitives]::ConvertTo(
                    $rawValue, $targetT)
            }
            catch {
                throw "Cannot convert value '$rawValue' (type $($rawValue.GetType().Name)) " +
                "to [$($targetT.Name)] for option '$key'."
            }

            $prop.SetValue($options, $converted, $null)
        }

        return $options
    }
}
