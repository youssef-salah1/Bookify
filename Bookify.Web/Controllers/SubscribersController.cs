using Bookify.Web.Services;
using Cover_to_Cover.Web.Core.Models;
using Cover_to_Cover.Web.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Cover_to_Cover.Web.Controllers
{
    public class SubscribersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IImageService _imageService;

        public SubscribersController(ApplicationDbContext context, IMapper mapper, IImageService imageService)
        {
            _context = context;
            _mapper = mapper;
            _imageService = imageService;
        }

        public IActionResult Index()
        {
            //var subscribers = _context.Subscribers.AsNoTracking().ToList();
            return View();
        }
        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = FillData();
            return View("Form", viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubscriberFormViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return View("Form", FillData(viewModel));

            var subscriber = _mapper.Map<Subscriber>(viewModel);
            if (viewModel.Image is not null)
            {
                var imageName = $"{Guid.NewGuid()}{Path.GetExtension(viewModel.Image.FileName)}";
                var (isUploaded, errorMessage) = await _imageService.UploadAsync(viewModel.Image, imageName, "/images/subscribers", hasThumbnail: true);
                if (!isUploaded)
                {
                    ModelState.AddModelError("Image", errorMessage!);
                    return View("Form", FillData(viewModel));
                }

                subscriber.ImageUrl = $"/images/subscribers/{imageName}";
                subscriber.ImageThumbnailUrl = $"/images/subscribers/thumb/{imageName}";
            }
            subscriber.CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

            _context.Add(subscriber);
            _context.SaveChanges();

            return RedirectToAction(nameof(Index), new { id = subscriber.Id });
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            var subscriber = _context.Subscribers.Find(id);
            if (subscriber is null)
                return NotFound();
            var model = _mapper.Map<SubscriberFormViewModel>(subscriber);
            var viewModel = FillData(model);
            return View("Form", viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SubscriberFormViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return View("Form", FillData(viewModel));

            var subscriber = _context.Subscribers.Find(viewModel.Id);
            if (subscriber is null)
                return NotFound();

            if (viewModel.Image is not null)
            {
                if (!String.IsNullOrEmpty(subscriber.ImageUrl))
                    _imageService.Delete(subscriber.ImageUrl, subscriber.ImageThumbnailUrl);
                var imageName = $"{Guid.NewGuid()}{Path.GetExtension(viewModel.Image.FileName)}";
                var (isUploaded, errorMessage) = await _imageService.UploadAsync(viewModel.Image, imageName, "/images/subscribers", hasThumbnail: true);
                if (!isUploaded)
                {
                    ModelState.AddModelError("Image", errorMessage!);
                    return View("Form", FillData(viewModel));
                }

                viewModel.ImageUrl = $"/images/subscribers/{imageName}";
                viewModel.ImageThumbnailUrl = $"/images/subscribers/thumb/{imageName}";
            }
            else
            {
                viewModel.ImageUrl = subscriber.ImageUrl; viewModel.ImageThumbnailUrl = subscriber.ImageThumbnailUrl;
            }
            subscriber = _mapper.Map(viewModel, subscriber);
            subscriber.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            subscriber.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return RedirectToAction(nameof(Index), new { id = subscriber.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Search(SearchFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var subscriber = _context.Subscribers
                            .SingleOrDefault(s =>
                                    s.Email == model.Value
                                || s.NationalId == model.Value
                                || s.MobileNumber == model.Value);

            var viewModel = _mapper.Map<SubscriberSearchResultViewModel>(subscriber);

            return PartialView("_Result", viewModel);
        }

        public IActionResult Details(int id)
        {
            var subscriber = _context.Subscribers
                .Include(s => s.Governorate)
                .Include(s => s.Area)
                .SingleOrDefault(s => s.Id == id);

            if (subscriber is null)
                return NotFound();

            var viewModel = _mapper.Map<SubscriberViewModel>(subscriber);

            return View(viewModel);
        }
        private SubscriberFormViewModel FillData(SubscriberFormViewModel? model = null)
        {
            var viewModel = model ?? new SubscriberFormViewModel();
            var governorates = _context.Governorates.AsNoTracking().Where(g => !g.IsDeleted).OrderBy(a => a.Name).ToList();
            viewModel.Governorates = _mapper.Map<IEnumerable<SelectListItem>>(governorates);
            if (viewModel.GovernorateId > 0)
            {
                var areas = _context.Areas.Where(A => A.GovernorateId == viewModel.GovernorateId && !A.IsDeleted).OrderBy(a => a.Name).ToList();
                viewModel.Areas = _mapper.Map<IEnumerable<SelectListItem>>(areas);
            }
            return viewModel;
        }
        [AjaxOnly]
        public IActionResult GetAreas(int governorateId)
        {
            var areas = _context.Areas
                    .Where(a => a.GovernorateId == governorateId && !a.IsDeleted)
                    .OrderBy(g => g.Name)
                    .ToList();

            return Ok(_mapper.Map<IEnumerable<SelectListItem>>(areas));
        }

        // Validation Part
        public IActionResult AllowItemNationalId(string NationalId, int Id)
        {
            var user = _context.Subscribers.SingleOrDefault(s => s.NationalId == NationalId);
            bool can = (user is null) || (user.Id == Id);
            return Json(can);
        }
        public IActionResult AllowItemMobileNumber(string MobileNumber, int Id)
        {
            var user = _context.Subscribers.SingleOrDefault(s => s.MobileNumber == MobileNumber);
            bool can = (user is null) || (user.Id == Id);
            return Json(can);
        }
        public IActionResult AllowItemEmail(string Email, int Id)
        {
            var user = _context.Subscribers.SingleOrDefault(s => s.Email == Email);
            bool can = (user is null) || (user.Id == Id);
            return Json(can);
        }
    }
}
