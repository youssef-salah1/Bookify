using System.IO;
using Bookify.Web.Core.Consts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bookify.Web.Controllers
{
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
            //var booksviewmodel = _mapper.Map<BookFormViewModel>(books);
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
            _context.Books.Add(book);
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
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
            var book = _context.Books.Include(c => c.Categories).SingleOrDefault(b => b.Id == model.Id);
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
            else if (!string.IsNullOrEmpty(book.ImageUrl)){
                model.ImageUrl = book.ImageUrl; model.ImageThumbUrl = book.ImageThumbUrl;
            }
            book = _mapper.Map(model, book);
            book.LastUpdatedOn = DateTime.Now;
            book.Categories.Clear();
            foreach (var c in model.SelectedCategories)
            {
                book.Categories.Add(new BookCategory { CategoryId = c });
            }
            _context.SaveChanges();
            return RedirectToAction(nameof(Index));
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
            var book = _context.Books.SingleOrDefault(b => b.Title == model.Title && b.AuthorId == model.AuthorId);
            var isAllowed = book is null || book.Id.Equals(model.Id);

            return Json(isAllowed);
        }
    }
}
