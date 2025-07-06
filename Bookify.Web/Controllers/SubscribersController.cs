using Bookify.Web.Services;
using Cover_to_Cover.Web.Core.Models;
using Cover_to_Cover.Web.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Cover_to_Cover.Web.Controllers
{
    //[Authorize(Roles = AppRoles.Reception)]
    public class SubscribersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IImageService _imageService;
        private readonly IDataProtector _dataProtector;
        public SubscribersController(ApplicationDbContext context, IMapper mapper, IImageService imageService, IDataProtectionProvider dataProtector)
        {
            _context = context;
            _mapper = mapper;
            _imageService = imageService;
            _dataProtector = dataProtector.CreateProtector("MySecureKey");
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

            var subscriberId = _dataProtector.Protect(subscriber.Id.ToString());

            return RedirectToAction(nameof(Details), new { id = subscriberId });
        }

        [HttpGet]
        public IActionResult Edit(string id)
        {
            var subscriberId = int.Parse(_dataProtector.Unprotect(id));
            var subscriber = _context.Subscribers.Find(subscriberId);
            if (subscriber is null)
                return NotFound();
            var model = _mapper.Map<SubscriberFormViewModel>(subscriber);

            var viewModel = FillData(model);
            viewModel.Key = id;

            return View("Form", viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SubscriberFormViewModel viewModel)
        {
            if (!ModelState.IsValid)
                return View("Form", FillData(viewModel));

            var subscriberId = int.Parse(_dataProtector.Unprotect(viewModel.Key));

            var subscriber = _context.Subscribers.Find(subscriberId);

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
                viewModel.ImageUrl = subscriber.ImageUrl;
                viewModel.ImageThumbnailUrl = subscriber.ImageThumbnailUrl;
            }
            subscriber = _mapper.Map(viewModel, subscriber);
            subscriber.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            subscriber.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return RedirectToAction(nameof(Details), new { id = viewModel.Key });
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

            if (subscriber is not null)
                viewModel.Key = _dataProtector.Protect(subscriber.Id.ToString());
            return PartialView("_Result", viewModel);
        }

        public IActionResult Details(string id)
        {

            var subscriberId = int.Parse(_dataProtector.Unprotect(id));

            var subscriber = _context.Subscribers
                .Include(s => s.Governorate)
                .Include(s => s.Area)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            var viewModel = _mapper.Map<SubscriberViewModel>(subscriber);
            viewModel.Key = id;

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
        public IActionResult AllowItemNationalId(string NationalId, string Key)
        {
            var subscriberId = 0;
            if (!String.IsNullOrEmpty(Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(Key));

            var user = _context.Subscribers.SingleOrDefault(s => s.NationalId == NationalId);
            bool can = (user is null) || (user.Id == subscriberId);
            return Json(can);
        }
        public IActionResult AllowItemMobileNumber(string MobileNumber, string Key)
        {
            var subscriberId = 0;
            if (!String.IsNullOrEmpty(Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(Key));

            var user = _context.Subscribers.SingleOrDefault(s => s.MobileNumber == MobileNumber);
            bool can = (user is null) || (user.Id == subscriberId);
            return Json(can);
        }
        public IActionResult AllowItemEmail(string Email, string Key)
        {
            var subscriberId = 0;
            if (!String.IsNullOrEmpty(Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(Key));

            var user = _context.Subscribers.SingleOrDefault(s => s.Email == Email);
            bool can = (user is null) || (user.Id == subscriberId);
            return Json(can);
        }
    }
}
