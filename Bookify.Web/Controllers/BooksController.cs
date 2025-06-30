using Bookify.Web.Core.Consts;
using Bookify.Web.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;

namespace Bookify.Web.Controllers
{
    [Authorize(Roles = AppRoles.Archive)]
    public class BooksController : Controller
    {
        readonly private ApplicationDbContext _context;
        readonly private IMapper _mapper;
        readonly private IWebHostEnvironment _webHostEnvironment;
        private List<string> _allowedExtensions = new() { ".jpg", ".jpeg", ".png" };
        private int _maxAllowedSize = 2097152;
        public BooksController(ApplicationDbContext context, IMapper mapper, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _mapper = mapper;
            _webHostEnvironment = webHostEnvironment;
        }
        public IActionResult Index()
        {
            var books = _context.Books.AsNoTracking().ToList();
            return View();
        }
        [HttpGet]
        public IActionResult Create()
        {
            return View("Form", FillData());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", FillData(model));
            var book = _mapper.Map<Book>(model);
            if (model.Image is not null)
            {
                var extension = Path.GetExtension(model.Image!.FileName);
                if (!_allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.NotAllowedExtension);
                }
                if (model.Image.Length > _maxAllowedSize)
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.MaxSize);
                }
                var imageName = $"{Guid.NewGuid()}{extension}";
                var path = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/books", imageName);
                using var stream = System.IO.File.Create(path);
                await model.Image.CopyToAsync(stream);
                book.ImageUrl = $"/images/books/{imageName}";
                stream.Dispose();
                var paththumb = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/books/thumb", imageName);
                using var image = Image.Load(model.Image.OpenReadStream());
                float ratio = image.Width / 200;
                var hi = image.Height / ratio;
                book.ImageThumbUrl = $"/images/books/thumb/{imageName}";
                image.Mutate(i => i.Resize(width: 200, height: (int)hi));
                image.Save(paththumb);
            }
            foreach (var c in model.SelectedCategories)
            {
                book.Categories.Add(new BookCategory { CategoryId = c });
            }
            book.CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            _context.Books.Add(book);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Details(int id)
        {
            var book = _context.Books
                .Include(I => I.Author)
                .Include(I => I.BookCopies)
                .Include(I => I.Categories)
                .ThenInclude(c => c.Category)
                .SingleOrDefault(B => B.Id == id);
            if (book is null)
                return NotFound();
            var Book = _mapper.Map<BookViewModel>(book);
            return View(Book);
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var model = _context.Books.Include(c => c.Categories).SingleOrDefault(b => b.Id == id);
            if (model is null) return NotFound();
            var viewmodel = _mapper.Map<BookFormViewModel>(model);
            viewmodel = FillData(viewmodel);
            viewmodel.SelectedCategories = model.Categories.Select(c => c.CategoryId).ToList();
            return View("Form", viewmodel);
        }

        [HttpPost]
        [AutoValidateAntiforgeryToken]
        public IActionResult Edit(BookFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", model);
            var book = _context.Books.Include(c => c.Categories)
                .Include(c => c.BookCopies)
                .SingleOrDefault(b => b.Id == model.Id);
            if (book is null) return NotFound();
            if (model.Image is not null)
            {
                if (!string.IsNullOrEmpty(book.ImageUrl))
                {
                    var oldpath = $"{_webHostEnvironment.WebRootPath}{book.ImageUrl}";
                    if (System.IO.File.Exists(oldpath))
                    {
                        System.IO.File.Delete(oldpath);
                    }
                }
                if (!string.IsNullOrEmpty(book.ImageThumbUrl))
                {
                    var oldpath = $"{_webHostEnvironment.WebRootPath}{book.ImageThumbUrl}";

                    if (System.IO.File.Exists(oldpath))
                    {
                        System.IO.File.Delete(oldpath);
                    }
                }
                var extension = Path.GetExtension(model.Image!.FileName);
                if (!_allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.NotAllowedExtension);
                }
                if (model.Image.Length > _maxAllowedSize)
                {
                    ModelState.AddModelError(nameof(model.Image), Errors.MaxSize);
                }
                var imageName = $"{Guid.NewGuid()}{extension}";
                var path = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/books", imageName);
                using var stream = System.IO.File.Create(path);
                model.Image.CopyTo(stream);
                model.ImageUrl = $"/images/books/{imageName}";
                stream.Dispose();
                using var image = Image.Load(model.Image.OpenReadStream());
                float ratio = image.Width / 200;
                var hi = image.Height / ratio;
                var paththumb = Path.Combine($"{_webHostEnvironment.WebRootPath}/images/books/thumb", imageName);
                model.ImageThumbUrl = $"/images/books/thumb/{imageName}";
                image.Mutate(i => i.Resize(width: 200, height: (int)hi));
                image.Save(paththumb);
            }
            else if (!string.IsNullOrEmpty(book.ImageUrl))
            {
                model.ImageUrl = book.ImageUrl; model.ImageThumbUrl = book.ImageThumbUrl;
            }
            book = _mapper.Map(model, book);
            book.LastUpdatedOn = DateTime.Now;
            book.Categories.Clear();
            foreach (var c in model.SelectedCategories)
            {
                book.Categories.Add(new BookCategory { CategoryId = c });
            }
            book.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            if (!book.IsAvailableForRental)
            {
                foreach (var copy in book.BookCopies)
                    copy.IsAvailableForRental = false;
            }
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public IActionResult GetData()
        {
            IQueryable<Book> books = _context.Books
                .Include(a => a.Author)
                .Include(c => c.Categories)
                .ThenInclude(c => c.Category);
            //selet Data
            var search = Request.Form["search[value]"];
            if(!String.IsNullOrEmpty(search))
                books = books.Where(b => b.Title.Contains(search) || b.Author!.Name.Contains(search));
            // sort data
            var SortByIndex = Request.Form["order[0][column]"];
            int num =(int.Parse(SortByIndex) + (int.Parse(SortByIndex) == 0 ? 1 : 0));
            string val = num.ToString();
            var SortBy = Request.Form[$"columns[{val}][name]"];
            var SortDirection = Request.Form["order[0][dir]"];
            books = books.OrderBy($"{SortBy} {SortDirection}");
            //skip , take and size
            var skip = int.Parse(Request.Form["start"]);
            var length = int.Parse(Request.Form["length"]); 
            var data = books.Skip(skip).Take(length).ToList();
            var recordsTotal = books.Count();
            // return
            var mapeddata = _mapper.Map<IEnumerable<BookViewModel>>(data);
            return Ok(new { recordsFiltered = recordsTotal, recordsTotal, data = mapeddata});
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            var book = _context.Books.Find(id);

            if (book is null)
                return NotFound();

            book.IsDeleted = !book.IsDeleted;
            book.LastUpdatedOn = DateTime.Now;
            book.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            _context.SaveChanges();

            return Ok();
        }

        private BookFormViewModel FillData(BookFormViewModel? model = null)
        {
            BookFormViewModel datamodel = (model is null ? new BookFormViewModel() : model);
            var authors = _context.Authors.Where(a => !a.IsDeleted).OrderBy(a => a.Name).AsNoTracking().ToList();
            var categories = _context.Categories.Where(a => !a.IsDeleted).OrderBy(a => a.Name).AsNoTracking().ToList();
            datamodel.Authors = _mapper.Map<IEnumerable<SelectListItem>>(authors);
            datamodel.Categories = _mapper.Map<IEnumerable<SelectListItem>>(categories);
            return datamodel;
        }
        public IActionResult AllowItem(BookFormViewModel model)
        {
            var book = _context.Books.SingleOrDefault(b => model.Id != b.Id && b.Title == model.Title && b.AuthorId == model.AuthorId);
            var isAllowed = book is null;

            return Json(isAllowed);
        }
    }
}
