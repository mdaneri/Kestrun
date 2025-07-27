// File: ApiKeyAuthenticationOptions.cs
using Microsoft.AspNetCore.Authentication;
using System;

 
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>HTTP header to read the key from.</summary>
    public string HeaderName { get; set; } = "X-Api-Key";

    /// <summary>Called to validate the raw key string. Return true if valid.</summary>
    public Func<string,bool> ValidateKey { get; set; } = _ => false;
}
