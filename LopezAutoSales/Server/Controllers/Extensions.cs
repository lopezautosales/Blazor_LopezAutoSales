using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;

namespace LopezAutoSales.Server.Controllers
{
    public static class Extensions
    {
        public static List<string> GetErrors(this ModelStateDictionary state)
        {
            List<string> errors = new List<string>();
            foreach (var entry in state.Values)
                foreach (var error in entry.Errors)
                    errors.Add(error.ErrorMessage);
            return errors;
        }
    }
}
