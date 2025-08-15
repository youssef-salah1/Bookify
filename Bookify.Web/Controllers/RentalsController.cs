using Cover_to_Cover.Web.Core.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers
{
    public class RentalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IDataProtector _dataProtector;
        public RentalsController(ApplicationDbContext context, IMapper mapper, IDataProtectionProvider dataProtector)
        {
            _context = context;
            _mapper = mapper;
            _dataProtector = dataProtector.CreateProtector("MySecureKey");
        }

        public IActionResult Create(string sKey)
        {

            var subscriberId = int.Parse(_dataProtector.Unprotect(sKey));
            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .Include(s => s.Rentals)
                .ThenInclude(r => r.RentalCopies)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            if (subscriber.IsBlackListed)
                return View("NotAllowedRental", Errors.BlackListedSubscriber);

            if (subscriber.Subscriptions.Last().EndDate < DateTime.Today.AddDays((int)RentalsConfigurations.RentalDuration))
                return View("NotAllowedRental", Errors.InactiveSubscriber);

            int currentRentals = subscriber.Rentals.SelectMany(s => s.RentalCopies).Count(r => !r.ReturnDate.HasValue);
            int rem = (int)RentalsConfigurations.MaxAllowedCopies - currentRentals;

            if (rem.Equals(0))
                return View("NotAllowedRental", Errors.MaxCopiesReached);

            var viewModel = new RentalFormViewModel
            {
                SubscriberKey = sKey,
                MaxAllowedCopies = rem
            };

            return View(viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(RentalFormViewModel viewmodel)
        {

            var subscriberId = int.Parse(_dataProtector.Unprotect(viewmodel.SubscriberKey));
            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .Include(s => s.Rentals)
                .ThenInclude(r => r.RentalCopies)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            if (subscriber.IsBlackListed)
                return View("NotAllowedRental", Errors.BlackListedSubscriber);

            if (subscriber.Subscriptions.Last().EndDate < DateTime.Today.AddDays((int)RentalsConfigurations.RentalDuration))
                return View("NotAllowedRental", Errors.InactiveSubscriber);

            int currentRentals = subscriber.Rentals.SelectMany(s => s.RentalCopies).Count(r => !r.ReturnDate.HasValue);
            int rem = (int)RentalsConfigurations.MaxAllowedCopies - currentRentals;

            if (rem.Equals(0))
                return View("NotAllowedRental", Errors.MaxCopiesReached);

            var selectedCopies = _context.BookCopies
                .Include(c => c.Rentals)
                .Include(c => c.Book)
                .Where(c => viewmodel.SelectedCopies.Contains(c.SerialNumber))
                .ToList();

            var subscriberRentals = _context.Rentals
                .Include(r => r.RentalCopies)
                .ThenInclude(c => c.BookCopy)
                .Where(s => s.SubscriberID == subscriberId)
                .SelectMany(r => r.RentalCopies)
                .Where(r => !r.ReturnDate.HasValue)
                .Select(r => r.BookCopy!.BookId)
                .ToList();

            List<RentalCopy> copies = new();
            foreach (var copy in selectedCopies)
            {
                if (!copy.IsAvailableForRental || !copy.Book.IsAvailableForRental)
                    return View("NotAllowedRental", Errors.NotAvilableRental);
                if (copy.Rentals.Any(c => !c.ReturnDate.HasValue))
                    return View("NotAllowedRental", Errors.CopyIsInRental);
                if (subscriberRentals.Any(bookId => bookId == copy.Book.Id))
                    return View("NotAllowedRental", $"This Subscriber already has same a copy of the same {copy.Book.Title} book");
                copies.Add(new RentalCopy { BookCopyId = copy.Id });
            }
            Rental rental = new()
            {
                RentalCopies = copies,
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value
            };
            subscriber.Rentals.Add(rental);
            _context.SaveChanges();
            return Ok();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GetCopyDetails(SearchFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();

            var copy = _context.BookCopies
                .Include(c => c.Book)
                .SingleOrDefault(c => c.SerialNumber.ToString() == model.Value && !c.IsDeleted && !c.Book!.IsDeleted);

            if (copy is null)
                return NotFound(Errors.InvalidSerialNumber);

            if (!copy.IsAvailableForRental || !copy.Book!.IsAvailableForRental)
                return BadRequest(Errors.NotAvilableRental);

            //TODO: check that copy is not in rental

            var copyIsRental = _context.RentalCopies.Any(r => r.BookCopyId == copy.Id && !r.ReturnDate.HasValue);

            if (copyIsRental)
                return BadRequest(Errors.CopyIsInRental);


            var viewModel = _mapper.Map<BookCopyViewModel>(copy);

            return PartialView("_CopyDetails", viewModel);
        }
    }
}
