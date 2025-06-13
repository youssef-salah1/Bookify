using Bookify.Web.Core.Consts;

namespace Bookify.Web.Core.ViewModels
{
    public class BookCopyViewModel
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public string BookTitle { get; set; } = null!;

        [Display(Name = "Is available for rental?")]
        public bool IsAvailableForRental { get; set; }

        [Display(Name = "Edition Number"),
            Range(1, 1000, ErrorMessage = Errors.InvalidRange)]
        public int EditionNumber { get; set; }
        public int SerialNumber { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedOn { get; set; } = DateTime.Now;
    }
}
