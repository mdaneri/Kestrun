using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RazorSample.Pages;

public class CSharpFormModel : PageModel
{
    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    public bool Submitted { get; private set; } = false;

    public void OnGet()
    {
        // Show form
    }

    public void OnPost()
    {
        Submitted = true;
        // Name and Email are auto-bound
    }
}
