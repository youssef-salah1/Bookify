using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers
{
    public class BooksController : Controller
    {
        readonly private ApplicationDbContext _context;
        readonly private IMapper _mapper;

        public BooksController(ApplicationDbContext context, IMapper mapper)
        {
            _context = context;
            _mapper = mapper;
        }
        public IActionResult Index()
        {
            var books = _context.Books.ToList();
            return View(books);
        }
    }
}
