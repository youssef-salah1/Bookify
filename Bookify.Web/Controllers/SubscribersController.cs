using Bookify.Web.Services;
using Cover_to_Cover.Web.Core.Consts;
using Cover_to_Cover.Web.Core.Models;
using Cover_to_Cover.Web.Core.ViewModels;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Numerics;
using System.Text.Encodings.Web;

namespace Cover_to_Cover.Web.Controllers
{
    [Authorize(Roles = AppRoles.Reception)]
    public class SubscribersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IImageService _imageService;
        private readonly IDataProtector _dataProtector;

        private readonly IEmailSender _emailSender;
        private readonly IEmailBodyBuilder _emailBodyBuilder;
        public SubscribersController(ApplicationDbContext context, IMapper mapper, IImageService imageService, IDataProtectionProvider dataProtector, IEmailSender emailSender, IEmailBodyBuilder emailBodyBuilder)
        {
            _context = context;
            _mapper = mapper;
            _imageService = imageService;
            _dataProtector = dataProtector.CreateProtector("MySecureKey");
            _emailSender = emailSender;
            _emailBodyBuilder = emailBodyBuilder;
        }

        public IActionResult Index()
        {
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

            Subscription subscription = new()
            {
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value,
                CreatedOn = DateTime.Now,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1),
            };
            subscriber.Subscriptions.Add(subscription);

            var placeholders = new Dictionary<string, string>()
            {
                { "imageUrl", "https://res.cloudinary.com/dyxgpclui/image/upload/v1747264748/icon-positive-vote-1_rdexez_acbkap.svg" },
                { "header", $"Hey {subscriber.FirstName}," },
                { "body", "thanks for joining Bookify 🤩" },
            };

            var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Email, placeholders);

            BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(
                subscriber.Email,
                "Welcome to Bookify",
                body));



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
                .Include(s => s.Subscriptions)
                .Include(s => s.Rentals)
                .ThenInclude(s => s.RentalCopies)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            var viewModel = _mapper.Map<SubscriberViewModel>(subscriber);
            viewModel.Key = id;

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RenewSubscription(string sKey)
        {
            if (String.IsNullOrEmpty(sKey))
                return BadRequest();

            var subscriberId = int.Parse(_dataProtector.Unprotect(sKey));
            var subscriber = _context.Subscribers
                .Include(s => s.Subscriptions)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            if (subscriber.IsBlackListed)
                return BadRequest();

            var lastSubscription = subscriber.Subscriptions.Last();
            var startDate = lastSubscription.EndDate < DateTime.Today
                            ? DateTime.Today
                            : lastSubscription.EndDate.AddDays(1);

            Subscription subscription = new()
            {
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value,
                CreatedOn = DateTime.Now,
                StartDate = startDate,
                EndDate = startDate.AddYears(1),
            };
            subscriber.Subscriptions.Add(subscription);

            _context.SaveChanges();

            var placeholders = new Dictionary<string, string>()
            {
                { "imageUrl", "https://res.cloudinary.com/dyxgpclui/image/upload/v1747265405/icon-positive-vote-1_rdexez_acbkap_fpsm6n.jpg" },
                { "header", $"Hey {subscriber.FirstName}," },
                { "body", $"your subscription has been renewed through {subscription.EndDate.ToString("d MMM, yyyy")} 🎉🎉" },
            };

            var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

            BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(
                subscriber.Email,
                "Bookify Subscription Renewal",
                body));

            var viewModel = _mapper.Map<SubscriptionViewModel>(subscription);

            return PartialView("_SubscriptionRow", viewModel);
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
