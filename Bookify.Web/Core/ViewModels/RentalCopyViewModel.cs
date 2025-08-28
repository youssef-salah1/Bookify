using Cover_to_Cover.Web.Core.Models;

namespace Bookify.Web.Core.ViewModels
{
    public class RentalCopyViewModel
    {
        public BookCopyViewModel? BookCopy { get; set; }
        public DateTime RentalDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime? ReturnDate { get; set; }
        public DateTime? ExtendedOn { get; set; }

        public int DelayInDays
        {
            get
            {
                int delay = 0;

                if (ReturnDate.HasValue && EndDate < ReturnDate)
                    delay = (int)(ReturnDate.Value - EndDate).TotalDays;
                else if (!ReturnDate.HasValue && EndDate < DateTime.Today)
                    delay = (int)(DateTime.Today - EndDate).TotalDays;
                return delay;
            }
        }
    }
}
