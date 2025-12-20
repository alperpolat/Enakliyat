using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Enakliyat.Web.Filters;

public class ValidationFilterAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => new
                {
                    Field = x.Key,
                    Message = e.ErrorMessage
                }))
                .ToList();

            if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // AJAX request
                context.Result = new JsonResult(new { success = false, errors })
                {
                    StatusCode = 400
                };
            }
            else
            {
                // Regular form submission - let the action handle it
                // Just add errors to ModelState, don't override the result
            }
        }

        base.OnActionExecuting(context);
    }
}

