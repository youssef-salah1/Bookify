using Cover_to_Cover.Web.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using UoN.ExpressiveAnnotations.NetCore.Attributes;

namespace Cover_to_Cover.Web.Core.ViewModels
{
    public class SubscriberFormViewModel
    {
        public int Id { get; set; }

        [Display(Name = "First Name")]
        [MaxLength(100, ErrorMessage = Errors.MaxLength)]
        [RegularExpression(RegexPatterns.DenySpecialCharacters, ErrorMessage = Errors.DenySpecialCharacters)]
        public string FirstName { get; set; } = null!;

        [Display(Name = "Last Name")]
        [MaxLength(100, ErrorMessage = Errors.MaxLength)]
        [RegularExpression(RegexPatterns.DenySpecialCharacters, ErrorMessage = Errors.DenySpecialCharacters)]
        public string LastName { get; set; } = null!;

        [Display(Name = "Date Of Birth")]
        [AssertThat("DateOfBirth <= Today()", ErrorMessage = Errors.NotAllowFutureDates)]
        public DateTime DateOfBirth { get; set; } = DateTime.Now;

        [Display(Name = "National Id")]
        [MaxLength(14, ErrorMessage = Errors.MaxLength)]
        [RegularExpression(RegexPatterns.NationalId, ErrorMessage = Errors.InvalidNationalId)]
        [Remote("AllowItemNationalId", null!, AdditionalFields = "Id", ErrorMessage = Errors.Duplicated)]
        public string NationalId { get; set; } = null!;

        [Display(Name = "Mobile Number")]
        [MaxLength(15, ErrorMessage = Errors.MaxLength)]
        [RegularExpression(RegexPatterns.MobileNumber, ErrorMessage = Errors.InvalidMobileNumber)]
        [Remote("AllowItemMobileNumber", null!, AdditionalFields = "Id", ErrorMessage = Errors.Duplicated)]

        public string MobileNumber { get; set; } = null!;

        public bool HasWhatsApp { get; set; }

        [MaxLength(150, ErrorMessage = Errors.MaxLength)]
        [Remote("AllowItemEmail", null!, AdditionalFields = "Id", ErrorMessage = Errors.Duplicated)]
        public string Email { get; set; } = null!;

        [RequiredIf("Id == 0", ErrorMessage = Errors.EmptyImage)]
        public IFormFile? Image { get; set; }

        public string? ImageUrl { get; set; }

        public string? ImageThumbnailUrl { get; set; }

        [Display(Name = "Area")]
        public int AreaId { get; set; }

        public IEnumerable<SelectListItem>? Areas { get; set; } = new List<SelectListItem>();

        [Display(Name = "Governorate")]
        public int GovernorateId { get; set; }

        public IEnumerable<SelectListItem>? Governorates { get; set; } = new List<SelectListItem>();

        [MaxLength(500)]
        public string Address { get; set; } = null!;

    }
}
