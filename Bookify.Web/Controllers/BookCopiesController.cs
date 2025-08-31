using Bookify.Web.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace Bookify.Web.Controllers
{
    [Authorize(Roles = AppRoles.Archive)]
    public class BookCopiesController(ApplicationDbContext context, IMapper mapper) : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
        [AjaxOnly]
        public IActionResult Create(int Id)
        {
            var book = context.Books.Find(Id);
            if (book is null) return BadRequest();
            BookCopyFormViewModel viewModel = new() { BookId = Id, ShowRentalInput = book.IsAvailableForRental };
            return PartialView("_Form" , viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(BookCopyFormViewModel viewModel)
        {
            //return Ok();
            if(!ModelState.IsValid) return BadRequest();
            var book = context.Books.Find(viewModel.Id);
            if (book is null) return NotFound();
            BookCopy bookCopy = new()
            {
                BookId = viewModel.Id,
                IsAvailableForRental = viewModel.IsAvailableForRental && book.IsAvailableForRental,
                EditionNumber = viewModel.EditionNumber,
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value
            };
            context.BookCopies.Add(bookCopy);
            context.SaveChanges();
            return PartialView("_BookCopyRow", bookCopy);
        }

        [AjaxOnly]
        public IActionResult Edit(int Id)
        {
            var copy = context.BookCopies.Find(Id);
            if (copy is null) return NotFound();
            var model = mapper.Map<BookCopyFormViewModel>(copy);
            var book = context.Books.Find(copy.BookId);
            if(book is null) return BadRequest();
            model.ShowRentalInput = book.IsAvailableForRental;
            return PartialView("_Form", model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(BookCopyFormViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest();
            var copy = context.BookCopies.Find(model.Id);
            if (copy is null) return NotFound();
            var book = context.Books.Find(copy.BookId);
            if (book is null) return BadRequest();
            copy.EditionNumber = model.EditionNumber;
            copy.IsAvailableForRental = model.IsAvailableForRental & book.IsAvailableForRental;
            copy.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            context.SaveChanges();
            return PartialView("_BookCopyRow", copy);
        }
        
        public IActionResult RentalHistory(int id)
        {
            var rentals = context.RentalCopies
                .Include(r => r.Rental)
                .ThenInclude(s => s.Subscriber)
                .Where(b => b.BookCopyId == id)
                .OrderByDescending(r => r.RentalDate)
                .ToList();
            var viewmodel = mapper.Map<IEnumerable<CopyHistoryViewModel>>(rentals);
            return View(viewmodel);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            var book = context.BookCopies.Find(id);

            if (book is null)
                return NotFound();

            book.IsDeleted = !book.IsDeleted;
            book.LastUpdatedOn = DateTime.Now;
            book.LastUpdatedOn = DateTime.Now;
            book.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            context.SaveChanges();

            return Ok();
        }
    }
}
