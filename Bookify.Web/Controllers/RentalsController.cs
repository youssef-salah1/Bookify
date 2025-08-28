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

            var (errorMessage, maxAllowedCopies) = ValidateSubscriber(subscriber);

            if (!string.IsNullOrEmpty(errorMessage))
                return View("NotAllowedRental", errorMessage);

            var viewModel = new RentalFormViewModel
            {
                SubscriberKey = sKey,
                MaxAllowedCopies = maxAllowedCopies
            };

            return View("Form", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(RentalFormViewModel viewmodel)
        {
            if (!ModelState.IsValid)
                return View("Form", viewmodel);

            var subscriberId = int.Parse(_dataProtector.Unprotect(viewmodel.SubscriberKey));

            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .Include(s => s.Rentals)
                .ThenInclude(r => r.RentalCopies)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            var (errorMessage, maxAllowedCopies) = ValidateSubscriber(subscriber);

            if (!string.IsNullOrEmpty(errorMessage))
                return View("NotAllowedRental", errorMessage);

            var (rentalsError, copies) = ValidateCopies(viewmodel.SelectedCopies, subscriberId);

            if (!string.IsNullOrEmpty(rentalsError))
                return View("NotAllowedRental", rentalsError);

            Rental rental = new()
            {
                RentalCopies = copies,
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value
            };
            subscriber.Rentals.Add(rental);
            _context.SaveChanges();
            return Ok();
        }

        public IActionResult Details(int id)
        {
            var rental = _context.Rentals
                .Include(r => r.RentalCopies)
                .ThenInclude(c => c.BookCopy)
                .ThenInclude(b => b!.Book)
                .SingleOrDefault(r => r.Id == id);
            if (rental is null)
                return NotFound();
            var viewModel = _mapper.Map<RentalViewModel>(rental);
            return View(viewModel);
        }

        public IActionResult Edit(int id)
        {
            var rental = _context.Rentals
                .Include(r => r.RentalCopies)
                .ThenInclude(c => c.BookCopy)
                .SingleOrDefault(r => r.Id == id);

            if (rental is null || rental.CreatedOn.Date != DateTime.Today)
                return NotFound();

            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .Include(s => s.Rentals)
                .ThenInclude(r => r.RentalCopies)
                .SingleOrDefault(s => s.Id == rental.SubscriberID);

            if (subscriber is null)
                return NotFound();

            var (errorMessage, maxAllowedCopies) = ValidateSubscriber(subscriber!, rental.Id);

            if (!string.IsNullOrEmpty(errorMessage))
                return View("NotAllowedRental", errorMessage);

            var currentCopiesId = rental.RentalCopies.Select(r => r.BookCopyId).ToList();

            var currentCopies = _context.BookCopies
                .Where(c => currentCopiesId.Contains(c.Id))
                .Include(b => b.Book)
                .ToList();

            var viewModel = new RentalFormViewModel
            {
                SubscriberKey = _dataProtector.Protect(subscriber.Id.ToString()),
                MaxAllowedCopies = maxAllowedCopies,
                CurrentCopies = _mapper.Map<IEnumerable<BookCopyViewModel>>(currentCopies)
            };

            return View("Form", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(RentalFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", model);

            var rental = _context.Rentals
                .Include(r => r.RentalCopies)
                .SingleOrDefault(r => r.Id == model.Id);

            if (rental is null || rental.CreatedOn.Date != DateTime.Today)
                return NotFound();

            var subscriberId = int.Parse(_dataProtector.Unprotect(model.SubscriberKey));

            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .Include(s => s.Rentals)
                .ThenInclude(r => r.RentalCopies)
                .SingleOrDefault(s => s.Id == subscriberId);

            var (errorMessage, maxAllowedCopies) = ValidateSubscriber(subscriber!, model.Id);

            if (!string.IsNullOrEmpty(errorMessage))
                return View("NotAllowedRental", errorMessage);

            var selectedCopies = _context.BookCopies
                .Include(c => c.Book)
                .Include(c => c.Rentals)
                .Where(c => model.SelectedCopies.Contains(c.SerialNumber))
                .ToList();

            var currentSubscriberRentals = _context.Rentals
                .Include(r => r.RentalCopies)
                .ThenInclude(c => c.BookCopy)
                .Where(r => r.SubscriberID == subscriberId && r.Id != model.Id)
                .SelectMany(r => r.RentalCopies)
                .Where(c => !c.ReturnDate.HasValue)
                .Select(c => c.BookCopy!.BookId)
                .ToList();

            List<RentalCopy> copies = new();

            foreach (var copy in selectedCopies)
            {
                if (!copy.IsAvailableForRental || !copy.Book!.IsAvailableForRental)
                    return View("NotAllowedRental", Errors.NotAvilableRental);

                if (copy.Rentals.Any(c => !c.ReturnDate.HasValue && c.RentalId != model.Id))
                    return View("NotAllowedRental", Errors.CopyIsInRental);

                if (currentSubscriberRentals.Any(bookId => bookId == copy.BookId))
                    return View("NotAllowedRental", $"This subscriber already has a copy for '{copy.Book.Title}' Book");

                copies.Add(new RentalCopy { BookCopyId = copy.Id });
            }

            if (copies.Count() > maxAllowedCopies)


                rental.RentalCopies = copies;
            rental.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            rental.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return RedirectToAction(nameof(Details), new { id = rental.Id });
        }

        public IActionResult Return(int id)
        {
            var rental = _context.Rentals
                .Include(r => r.RentalCopies)
                .ThenInclude(c => c.BookCopy)
                .ThenInclude(b => b!.Book)
                .SingleOrDefault(r => r.Id == id);

            if (rental is null || rental.CreatedOn.Date == DateTime.Today)
                return NotFound();

            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .SingleOrDefault(s => s.Id == rental.SubscriberID);

            if (subscriber is null)
                return NotFound();

            var viewModel = new RentalReturnViewModel()
            {
                Id = id,
                Copies = _mapper.Map<IList<RentalCopyViewModel>>(rental.RentalCopies.Where(r => !r.ReturnDate.HasValue)),
                SelectedCopies =
                    rental.RentalCopies.Where(r => !r.ReturnDate.HasValue).Select(r => new ReturnCopyViewModel
                        { Id = r.BookCopyId, IsReturned = r.ExtendedOn.HasValue ? false : null }).ToList(),
                AllowExtend = (!subscriber!.IsBlackListed &&
                               subscriber.Subscriptions.Last().EndDate >=
                               rental.StartDate.AddDays((int)RentalsConfigurations.RentalDuration * 2)
                               && rental.StartDate.AddDays((int)RentalsConfigurations.RentalDuration) >= DateTime.Today)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Return(RentalReturnViewModel model)
        {
            var rental = _context.Rentals
                .Include(r => r.RentalCopies)
                .ThenInclude(c => c.BookCopy)
                .ThenInclude(b => b!.Book)
                .SingleOrDefault(r => r.Id == model.Id);

            if (rental is null || rental.CreatedOn.Date == DateTime.Today)
                return NotFound();

            model.Copies = _mapper.Map<IList<RentalCopyViewModel>>(rental.RentalCopies.Where(r => !r.ReturnDate.HasValue));

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .SingleOrDefault(r => r.Id == rental.SubscriberID);

            if (model.SelectedCopies.Any(c => c.IsReturned.HasValue && !c.IsReturned.Value))
            {
                if (subscriber!.IsBlackListed)
                {
                    ModelState.AddModelError("", "This subscriber is blacklisted and cannot extend rentals.");
                    return View(model);
                }

                if (subscriber.Subscriptions.Last().EndDate <
                    rental.StartDate.AddDays((int)RentalsConfigurations.RentalDuration * 2))
                {
                    ModelState.AddModelError("",
                        "This subscriber subscription will be inactive before the extended return date.");
                    return View(model);
                }

                if (rental.StartDate.AddDays((int)RentalsConfigurations.RentalDuration) < DateTime.Today)
                {
                    ModelState.AddModelError("", "The rental period has already passed, cannot extend.");
                    return View(model);
                }
            }

            var isUpdated = false;

            foreach (var copy in model.SelectedCopies)
            {
                if (!copy.IsReturned.HasValue) continue;
                var copyInDb = rental.RentalCopies.SingleOrDefault(c => c.BookCopyId == copy.Id);
                if (copyInDb is null) continue;
                if (copy.IsReturned.Value)
                {
                    if (copyInDb.ReturnDate.HasValue) continue;
                    copyInDb.ReturnDate = DateTime.Now;
                    isUpdated = true;
                }
                else if (!copy.IsReturned.Value)
                {
                    if (copyInDb.ExtendedOn.HasValue) continue;
                    
                    copyInDb.ExtendedOn = DateTime.Now;
                    copyInDb.EndDate = copyInDb.RentalDate.AddDays((int)RentalsConfigurations.RentalDuration * 2);
                    isUpdated = true;
                }
            }

            if (isUpdated)
            {
                rental.LastUpdatedOn = DateTime.Now;
                rental.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                rental.PenaltyPaid = model.PenaltyPaid;
                _context.SaveChanges();
            }
            
            return RedirectToAction(nameof(Details) , new { id = rental.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAsDeleted(int id)
        {
            var rental = _context.Rentals.Find(id);
            if (rental is null || rental.CreatedOn.Date == DateTime.Today)
                return NotFound();
            rental.IsDeleted = true;
            rental.LastUpdatedOn = DateTime.Now;
            rental.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            _context.SaveChanges();
            var cnt = _context.Rentals.Count(r => r.Id == id);
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

            var copyIsRental = _context.RentalCopies.Any(r =>
                r.BookCopyId == copy.Id && !r.ReturnDate.HasValue &&
                _context.Rentals.Any(c => r.RentalId == c.Id && c.IsDeleted == false));

            if (copyIsRental)
                return BadRequest(Errors.CopyIsInRental);


            var viewModel = _mapper.Map<BookCopyViewModel>(copy);

            return PartialView("_CopyDetails", viewModel);
        }

        private (string errorMessage, int? maxAllowedCopies) ValidateSubscriber(Subscriber subscriber,
            int? rentalId = null)
        {
            if (subscriber.IsBlackListed)
                return (errorMessage: Errors.BlackListedSubscriber, maxAllowedCopies: null);

            if (subscriber.Subscriptions.Last().EndDate <
                DateTime.Today.AddDays((int)RentalsConfigurations.RentalDuration))
                return (errorMessage: Errors.InactiveSubscriber, maxAllowedCopies: null);

            var currentRentals = subscriber.Rentals
                .Where(r => rentalId == null || r.Id != rentalId)
                .SelectMany(r => r.RentalCopies)
                .Count(c => !c.ReturnDate.HasValue);

            var availableCopiesCount = (int)RentalsConfigurations.MaxAllowedCopies - currentRentals;

            if (availableCopiesCount.Equals(0))
                return (errorMessage: Errors.MaxCopiesReached, maxAllowedCopies: null);

            return (errorMessage: string.Empty, maxAllowedCopies: availableCopiesCount);
        }

        private (string errorMessage, ICollection<RentalCopy> copies) ValidateCopies(IEnumerable<int> selectedSerials,
            int subscriberId, int? rentalId = null)
        {
            var selectedCopies = _context.BookCopies
                .Include(c => c.Book)
                .Include(c => c.Rentals)
                .Where(c => selectedSerials.Contains(c.SerialNumber))
                .ToList();

            var currentSubscriberRentals = _context.Rentals
                .Include(r => r.RentalCopies)
                .ThenInclude(c => c.BookCopy)
                .Where(r => r.SubscriberID == subscriberId && (rentalId == null || r.Id != rentalId))
                .SelectMany(r => r.RentalCopies)
                .Where(c => !c.ReturnDate.HasValue)
                .Select(c => c.BookCopy!.BookId)
                .ToList();

            List<RentalCopy> copies = new();

            foreach (var copy in selectedCopies)
            {
                if (!copy.IsAvailableForRental || !copy.Book!.IsAvailableForRental)
                    return (errorMessage: Errors.NotAvilableRental, copies);

                if (copy.Rentals.Any(c => !c.ReturnDate.HasValue && (rentalId == null || c.RentalId != rentalId)))
                    return (errorMessage: Errors.CopyIsInRental, copies);

                if (currentSubscriberRentals.Any(bookId => bookId == copy.BookId))
                    return (errorMessage: $"This subscriber already has a copy for '{copy.Book.Title}' Book", copies);

                copies.Add(new RentalCopy { BookCopyId = copy.Id });
            }

            return (errorMessage: string.Empty, copies);
        }
    }
}