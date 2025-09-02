using Microsoft.AspNetCore.Authorization;

namespace Bookify.Web.Controllers;

[Authorize]
public class DashBoardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;

    public DashBoardController(ApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public IActionResult Index()
    {
        var numberOfBookCopies = _context.BookCopies.Count(b => !b.IsDeleted);
        var numberOfSubscriber = _context.Subscribers.Count(s => !s.IsDeleted);

        numberOfBookCopies = (numberOfBookCopies > 10 ? numberOfBookCopies / 10 * 10 : numberOfBookCopies);

        var lastAddedBooks = _context.Books
            .Include(a => a.Author)
            .Where(b => !b.IsDeleted)
            .OrderByDescending(b => b.Id)
            .Take(8)
            .ToList();

        var topBooks = _context.RentalCopies
            .Include(r => r.BookCopy)
            .ThenInclude(b => b.Book)
            .ThenInclude(a => a.Author)
            .GroupBy(b => new
            {
                b.BookCopy!.Book!.Id,
                b.BookCopy.Book.Title,
                b.BookCopy.Book.ImageThumbUrl,
                AuthorName = b.BookCopy.Book.Author!.Name
            })
            .Select(c => new
            {
                BookId = c.Key.Id,
                c.Key.Title,
                c.Key.ImageThumbUrl,
                c.Key.AuthorName,
                RentalCount = c.Count()
            })
            .OrderByDescending(c => c.RentalCount)
            .Take(6)
            .Select(b => new BookViewModel
            {
                Id = b.BookId,
                Title = b.Title,
                ImageThumbUrl = b.ImageThumbUrl,
                Author = b.AuthorName
            })
            .ToList();

        var model = new DashboardViewModel
        {
            NumberOfCopies = numberOfBookCopies,
            NumberOfSubscribers = numberOfSubscriber,
            LastAddedBooks = _mapper.Map<IEnumerable<BookViewModel>>(lastAddedBooks),
            TopBooks = topBooks
        };


        return View(model);
    }

    [AjaxOnly]
    public IActionResult GetRentalsPerDay(DateTime? startDate, DateTime? endDate)
    {
        startDate ??= DateTime.Today.AddDays(-29);
        endDate ??= DateTime.Today;

        var data = _context.RentalCopies
            .Where(c => c.RentalDate >= startDate && c.RentalDate <= endDate)
            .GroupBy(c => new { Date = c.RentalDate })
            .Select(g => new ChartItemViewModel
            {
                Label = g.Key.Date.ToString("d MMM"),
                Value = g.Count().ToString()
            })
            .ToList();

        List<ChartItemViewModel> figures = new();

        for (var day = startDate; day <= endDate; day = day.Value.AddDays(1))
        {
            var dayData = data.SingleOrDefault(d => d.Label == day.Value.ToString("d MMM"));

            ChartItemViewModel item = new()
            {
                Label = day.Value.ToString("d MMM"),
                Value = dayData is null ? "0" : dayData.Value
            };

            figures.Add(item);
        }


        return Ok(figures);
    }

    [AjaxOnly]
    public IActionResult GetSubscribersPerCity()
    {
        var data = _context.Subscribers
            .Include(s => s.Governorate)
            .Where(s => !s.IsDeleted)
            .GroupBy(s => new { GovernorateName = s.Governorate!.Name })
            .Select(g => new ChartItemViewModel
            {
                Label = g.Key.GovernorateName,
                Value = g.Count().ToString()
            })
            .ToList();

        return Ok(data);
    }
}