using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Bit.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Scim.Controllers
{
    public class BaseController : Controller
    {
        protected ICollection<T> FilterResources<T>(ICollection<T> resources, string filter) where T : IExternal
        {
            if(!string.IsNullOrWhiteSpace(filter))
            {
                var filterMatch = Regex.Match(filter, "(\\w+) eq \"([^\"]*)\"");
                if(filterMatch.Success && filterMatch.Groups.Count > 2)
                {
                    var searchKey = filterMatch.Groups[1].Value;
                    var searchValue = filterMatch.Groups[2].Value;

                    if(!string.IsNullOrWhiteSpace(searchKey) && !string.IsNullOrWhiteSpace(searchValue))
                    {
                        var searchKeyLower = searchKey.ToLowerInvariant();
                        if(searchKeyLower == "externalid")
                        {
                            resources = resources.Where(u => u.ExternalId == searchValue).ToList();
                        }
                    }
                }
            }

            return resources;
        }
    }
}
