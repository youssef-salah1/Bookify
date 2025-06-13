using Bookify.Web.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Bookify.Web.Controllers
{
    public class BookCopiesController : Controller
    {
        readonly private ApplicationDbContext _context;
        readonly private IMapper _mapper;
        public BookCopiesController(ApplicationDbContext context , IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public IActionResult Index()
        {
            return View();
        }
        [AjaxOnly]
        public IActionResult Create(int Id)
        {
            var book = _context.Books.Find(Id);
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
            var book = _context.Books.Find(viewModel.Id);
            if (book is null) return NotFound();
            BookCopy bookCopy = new()
            {
                BookId = viewModel.Id,
                IsAvailableForRental = viewModel.IsAvailableForRental && book.IsAvailableForRental,
                EditionNumber = viewModel.EditionNumber
            };
            _context.BookCopies.Add(bookCopy);
            _context.SaveChanges();
            return PartialView("_BookCopyRow", bookCopy);
        }

        [AjaxOnly]
        public IActionResult Edit(int Id)
        {
            var copy = _context.BookCopies.Find(Id);
            if (copy is null) return NotFound();
            var model = _mapper.Map<BookCopyFormViewModel>(copy);
            var book = _context.Books.Find(copy.BookId);
            if(book is null) return BadRequest();
            model.ShowRentalInput = book.IsAvailableForRental;
            return PartialView("_Form", model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(BookCopyFormViewModel model)
        {
            if (!ModelState.IsValid) return BadRequest();
            var copy = _context.BookCopies.Find(model.Id);
            if (copy is null) return NotFound();
            var book = _context.Books.Find(copy.BookId);
            if (book is null) return BadRequest();
            copy.EditionNumber = model.EditionNumber;
            copy.IsAvailableForRental = model.IsAvailableForRental & book.IsAvailableForRental;
            _context.SaveChanges();
            return PartialView("_BookCopyRow", copy);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            var book = _context.BookCopies.Find(id);

            if (book is null)
                return NotFound();

            book.IsDeleted = !book.IsDeleted;
            book.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return Ok();
        }
    }
}
