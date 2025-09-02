using Bookify.Web.Core.Utilities;
using Cover_to_Cover.Web.Core.Models;

namespace Bookify.Web.Core.ViewModels
{
    public class RentalsReportViewModel
    {
        public string Duration { get; set; } = null!;
        public PaginatedList<RentalCopy> Rentals { get; set; }
    }
}