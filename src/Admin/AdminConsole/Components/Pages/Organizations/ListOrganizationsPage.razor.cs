using System.ComponentModel.DataAnnotations;
using System.Net;
using Bit.Admin.Models;
using Bit.Core.AdminConsole.Entities;
using Microsoft.AspNetCore.Components;

namespace Bit.Admin.AdminConsole.Components.Pages.Organizations;

public partial class ListOrganizationsPage : ComponentBase
{
    public const string SearchFormName = "search-form";

    [SupplyParameterFromForm(FormName = SearchFormName)]
    public SearchFormModel? SearchForm { get; set; }

    public ViewModel Model { get; } = new();

    protected override async Task OnInitializedAsync()
    {
        SearchForm ??= new SearchFormModel();
        Model.SelfHosted = GlobalSettings.SelfHosted;
        Model.Action = GlobalSettings.SelfHosted ? "View" : "Edit";
    }

    private async Task OnSearchAsync()
    {
        if (SearchForm!.Page < 1)
        {
            SearchForm.Page = 1;
        }

        if (SearchForm.Count < 1)
        {
            SearchForm.Count = 1;
        }

        var encodedName = WebUtility.HtmlEncode(SearchForm.Name);
        var skip = (SearchForm.Page - 1) * SearchForm.Count;
        Model.Items = (List<Organization>)await OrganizationRepository.SearchAsync(
            encodedName,
            SearchForm.Email,
            SearchForm.Paid, skip, SearchForm.Count);

        Model.Page = SearchForm.Page;
        Model.Count = SearchForm.Count;
    }

    public class SearchFormModel
    {
        private string? _name;

        public string? Name
        {
            get
            {
                return string.IsNullOrWhiteSpace(_name) ? null : _name;
            }
            set
            {
                _name = value;
            }
        }

        private string? _email;

        [EmailAddress]
        public string? Email
        {
            get
            {
                return string.IsNullOrWhiteSpace(_email) ? null : _email;
            }
            set
            {
                _email = value;
            }
        }

        public bool? Paid { get; set; }

        public int Page { get; set; } = 1;

        public int Count { get; set; } = 25;
    }

    public class ViewModel : PagedModel<Organization>
    {
        public string Action { get; set; }
        public bool SelfHosted { get; set; }
    }
}

